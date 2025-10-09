using System;
using System.Windows.Forms;

namespace OfflineOps
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!SingleInstance.Start())
            {
                SingleInstance.ShowFirstInstance();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!LicenseManager.IsActivated())
            {
                frmLogin _frmLogin = new frmLogin();
                DialogResult result = _frmLogin.ShowDialog();
                if (result != DialogResult.OK)
                {
                    MessageBox.Show("Application must be activated to run.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SingleInstance.Stop();
                    return;
                }
            }

            if (!DatabaseManager.Initialize())
            {
                SingleInstance.Stop();
                return;
            }

            Application.Run(new frmMain());
            SingleInstance.Stop();
        }
    }
}