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
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly ToolStripLabel guardStatusItem;
        private readonly System.Threading.Timer timer;
        private readonly PolicyStore policyStore;
        private readonly EnforcementService enforcement;
        private int enforcing;
        private DateTime lastModernRefresh = DateTime.MinValue;

        public GuardContext()
        {
            policyStore = new PolicyStore();
            enforcement = new EnforcementService(policyStore);
            trayIcon = LoadApplicationIcon();
            tray = new NotifyIcon();
            tray.Icon = trayIcon;
            tray.Text = "右键小守卫 · 守护中 · 双击打开";
            tray.Visible = true;
            trayMenu = BuildTrayMenu(out guardStatusItem);
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { OpenMain(); };
            tray.BalloonTipClicked += delegate { OpenMain(); };
            UpdateGuardStatus();
            timer = new System.Threading.Timer(delegate { Enforce(false); }, null, 100, 1500);
        }

        private bool Enforce(bool forceModern)
        {
            if (Interlocked.Exchange(ref enforcing, 1) != 0) return false;
            try
            {
                bool refreshModern = forceModern ||
                                     DateTime.UtcNow - lastModernRefresh > TimeSpan.FromSeconds(30);
                enforcement.EnforceAll(refreshModern);
                if (refreshModern)
                {
                    lastModernRefresh = DateTime.UtcNow;
                }
                return true;
            }
            catch { return false; }
            finally { Interlocked.Exchange(ref enforcing, 0); }
        }

        private ContextMenuStrip BuildTrayMenu(out ToolStripLabel status)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.White;
            menu.ForeColor = Color.FromArgb(41, 48, 73);
            menu.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            menu.ImageScalingSize = new Size(20, 20);
            menu.MinimumSize = new Size(238, 0);
            menu.Padding = new Padding(7, 7, 7, 7);
            menu.Renderer = new ToolStripProfessionalRenderer(new TrayColorTable());

            ToolStripMenuItem open = new ToolStripMenuItem(
                "打开右键小守卫", trayIcon.ToBitmap(), delegate { OpenMain(); });
            open.Font = new Font(menu.Font, FontStyle.Bold);
            open.Padding = new Padding(3, 4, 3, 4);
            menu.Items.Add(open);

            status = new ToolStripLabel();
            status.ForeColor = Color.FromArgb(52, 167, 133);
            status.Image = BuildBadgeImage(Color.FromArgb(80, 197, 160), "✓");
            status.Padding = new Padding(3, 2, 3, 5);
            menu.Items.Add(status);

            ToolStripSeparator firstLine = new ToolStripSeparator();
            firstLine.Margin = new Padding(0, 3, 0, 3);
            menu.Items.Add(firstLine);

            ToolStripMenuItem enforce = new ToolStripMenuItem(
                "立即检查并重新压制",
                BuildBadgeImage(Color.FromArgb(124, 119, 255), "↻"),
                delegate { EnforceFromMenu(); });
            enforce.Padding = new Padding(3, 4, 3, 4);
            menu.Items.Add(enforce);

            ToolStripSeparator secondLine = new ToolStripSeparator();
            secondLine.Margin = new Padding(0, 3, 0, 3);
            menu.Items.Add(secondLine);

            ToolStripMenuItem exit = new ToolStripMenuItem(
                "退出后台守护",
                BuildBadgeImage(Color.FromArgb(255, 143, 181), "×"),
                delegate { ExitGuard(); });
            exit.Padding = new Padding(3, 4, 3, 4);
            menu.Items.Add(exit);
            return menu;
        }

        private void EnforceFromMenu()
        {
            guardStatusItem.Text = "正在检查受保护的右键菜单…";
            bool completed = Enforce(true);
            UpdateGuardStatus();
            if (!completed) return;
            tray.ShowBalloonTip(1800, "右键小守卫检查完成",
                "受保护的右键菜单规则已经重新确认。",
                ToolTipIcon.Info);
        }

        private void UpdateGuardStatus()
        {
            int count = 0;
            try
            {
                PolicyDocument policy = policyStore.Load();
                if (policy != null && policy.Rules != null)
                    count = policy.Rules.Count;
            }
            catch { }
            guardStatusItem.Text = count == 0
                ? "守护运行中 · 暂无关闭项目"
                : "守护运行中 · 已保护 " + count + " 项";
            string tip = count == 0
                ? "右键小守卫 · 守护中 · 双击打开"
                : "右键小守卫 · 守护中 · 已保护 " + count + " 项";
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                string path = typeof(GuardContext).Assembly.Location;
                Icon extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted != null)
                {
                    Icon clone = (Icon)extracted.Clone();
                    extracted.Dispose();
                    return clone;
                }
            }
            catch { }
            return (Icon)SystemIcons.Shield.Clone();
        }

        private static Bitmap BuildBadgeImage(Color background, string glyph)
        {
            Bitmap image = new Bitmap(20, 20);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.SmoothingMode =
                    System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush fill = new SolidBrush(background))
                    graphics.FillEllipse(fill, 1, 1, 18, 18);
                using (Font symbol = new Font("Segoe UI Symbol", 10F,
                    FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush ink = new SolidBrush(Color.White))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    graphics.DrawString(glyph, symbol, ink,
                        new RectangleF(0, 0, 20, 19), format);
                }
            }
            return image;
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
                tray.Visible = false;
                tray.Dispose();
                trayMenu.Dispose();
                trayIcon.Dispose();
                timer.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class TrayColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder
            {
                get { return Color.FromArgb(225, 226, 238); }
            }

            public override Color MenuItemBorder
            {
                get { return Color.FromArgb(214, 210, 255); }
            }

            public override Color MenuItemSelected
            {
                get { return Color.FromArgb(238, 237, 255); }
            }

            public override Color MenuItemSelectedGradientBegin
            {
                get { return Color.FromArgb(238, 237, 255); }
            }

            public override Color MenuItemSelectedGradientEnd
            {
                get { return Color.FromArgb(246, 239, 255); }
            }

            public override Color ImageMarginGradientBegin
            {
                get { return Color.White; }
            }

            public override Color ImageMarginGradientMiddle
            {
                get { return Color.White; }
            }

            public override Color ImageMarginGradientEnd
            {
                get { return Color.White; }
            }

            public override Color SeparatorDark
            {
                get { return Color.FromArgb(233, 234, 244); }
            }

            public override Color SeparatorLight
            {
                get { return Color.White; }
            }
        }
    }
}
