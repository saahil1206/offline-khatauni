using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfflineOps
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
            this.Load += frmMain_Load;
            deActivateToolStripMenuItem.Click += deActivateToolStripMenuItem_Click;
            bazarsToolStripMenuItem.Click += bazarsToolStripMenuItem_Click;
        }

        private void bazarsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmBazar frm = new frmBazar();
            frm.ShowDialog();
        }

        private void deActivateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Are you sure to De-Activate Application.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (dr == DialogResult.Yes)
            {
                LicenseManager.DeActivate();
                Application.Exit();
            }
        }

        private async void frmMain_Load(object sender, EventArgs e)
        {
            ApiHelper apiHelper = new ApiHelper();

            await apiHelper.SyncBazarData();
            await apiHelper.SyncPlayerData(null);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == SingleInstance.WM_SHOWFIRSTINSTANCE)
            {
                this.WindowState = FormWindowState.Normal;
                WinApi.ShowToFront(this.Handle);
            }
            base.WndProc(ref message);
        }
    }
}