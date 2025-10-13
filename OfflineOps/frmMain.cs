using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
            playersToolStripMenuItem.Click += PlayersToolStripMenuItem_Click;
            txtPlayer.SelectedIndexChanged += txtPlayer_SelectedIndexChanged;
            txtGame.SelectedIndexChanged += txtGame_SelectedIndexChanged;
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

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == SingleInstance.WM_SHOWFIRSTINSTANCE)
            {
                this.WindowState = FormWindowState.Normal;
                WinApi.ShowToFront(this.Handle);
            }
            base.WndProc(ref message);
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

        private void frmMain_Load(object sender, EventArgs e)
        {
            LoadPlayers();
            this.ActiveControl = null;
        }

        private void LoadPlayers()
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT id, username FROM users"); cmd.CommandType = CommandType.Text;
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            txtPlayer.Items.Clear(); txtPlayer.Items.Add(new ComboItem("-- Select Player --", 0));
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                txtPlayer.Items.Add(new ComboItem(dt.Rows[i]["username"].ToString(), dt.Rows[i]["id"].ToString()));
            }
            txtPlayer.SelectedIndex = 0;
        }


        private void txtPlayer_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboItem selectedItem = txtPlayer.SelectedItem as ComboItem; long user_id = 0;
            if (selectedItem != null) { long.TryParse(selectedItem.Value.ToString(), out user_id); }
            LoadBalance(user_id);
            LoadGames(user_id);
        }

        private void LoadBalance(long user_id)
        {

        }

        private void LoadGames(long user_id)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT b.id, b.bazar_name FROM user_games g INNER JOIN bazar b ON g.game_id = b.id WHERE g.user_id = @user_id"); cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("@user_id", user_id);
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            txtGame.Items.Clear(); txtGame.Items.Add(new ComboItem("-- Select Game --", 0));
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                txtGame.Items.Add(new ComboItem(dt.Rows[i]["bazar_name"].ToString(), dt.Rows[i]["id"].ToString()));
            }
            txtGame.SelectedIndex = 0;
        }

        private void txtGame_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}