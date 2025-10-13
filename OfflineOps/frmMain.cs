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
            playersToolStripMenuItem.Click += PlayersToolStripMenuItem_Click; ;
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Ctrl + B
            if (keyData == (Keys.Control | Keys.B))
            {
                OpenBazarWindow();
                return true;
            }

            // Ctrl + P
            if (keyData == (Keys.Control | Keys.P))
            {
                OpenPlayerWindow();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PlayersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPlayerWindow();
        }

        private void bazarsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenBazarWindow();
        }

        private void OpenBazarWindow()
        {
            frmBazar frm = new frmBazar();
            frm.ShowDialog();
        }

        private void OpenPlayerWindow()
        {
            frmPlayer frm = new frmPlayer();
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

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}