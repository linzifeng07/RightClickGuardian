using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace RightClickGuardian
{
    public sealed class GuardContext : ApplicationContext
    {
        private readonly NotifyIcon tray;
        private readonly System.Threading.Timer timer;
        private readonly EnforcementService enforcement;
        private int enforcing;
        private DateTime lastModernRefresh = DateTime.MinValue;

        public GuardContext()
        {
            enforcement = new EnforcementService(new PolicyStore());
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Shield;
            tray.Text = "右键小守卫 · 强制守护中";
            tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开右键小守卫", null, delegate { OpenMain(); });
            menu.Items.Add("立即重新压制", null, delegate { Enforce(true); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出守护", null, delegate { ExitGuard(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { OpenMain(); };
            timer = new System.Threading.Timer(delegate { Enforce(false); }, null, 100, 1500);
        }

        private void Enforce(bool forceModern)
        {
            if (Interlocked.Exchange(ref enforcing, 1) != 0) return;
            try
            {
                bool refreshModern = forceModern ||
                                     DateTime.UtcNow - lastModernRefresh > TimeSpan.FromSeconds(30);
                enforcement.EnforceAll(refreshModern);
                if (refreshModern)
                {
                    lastModernRefresh = DateTime.UtcNow;
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref enforcing, 0); }
        }

        private static void OpenMain()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Process.GetCurrentProcess().MainModule.FileName;
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch { }
        }

        private void ExitGuard()
        {
            tray.Visible = false;
            timer.Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
