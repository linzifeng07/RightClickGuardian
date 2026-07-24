using System;
using System.Linq;
using System.Threading;

namespace RightClickGuardian
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool guardMode = args != null &&
                             args.Any(value => string.Equals(value, "--guard",
                                 StringComparison.OrdinalIgnoreCase));
            if (guardMode)
            {
                RunGuard();
                return;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, @"Global\RightClickGuardian.Main", out created))
            {
                if (!created)
                {
                    System.Windows.MessageBox.Show(
                        "右键小守卫已经打开啦 ฅ^•ﻌ•^ฅ",
                        "右键小守卫",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }
                System.Windows.Application application = new System.Windows.Application();
                application.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
                application.Run(new MainWindow());
            }
        }

        private static void RunGuard()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, @"Global\RightClickGuardian.Guard", out created))
            {
                if (!created) return;
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                System.Windows.Forms.Application.Run(new GuardContext());
            }
        }
    }
}
