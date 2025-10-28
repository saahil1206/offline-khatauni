using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfflineOps
{
    public partial class frmPlayer : Form
    {

        private bool isSyncInProgress = false;
        private CancellationTokenSource cancellationTokenSource;
        public event Action OnSyncCompleted;

        public frmPlayer()
        {
            InitializeComponent();
            this.Load += frmPlayer_Load;
            this.FormClosing += frmBazar_FormClosing;
            btnSync.Click += btnSync_Click;
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

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel; this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private void frmBazar_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isSyncInProgress)
            {
                var result = MessageBox.Show(
                    "Sync is in progress. Closing now may result in incomplete data.\n\nDo you want to close anyway?",
                    "Sync In Progress", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No)
                {
                    e.Cancel = true; // Block closing
                }
                else
                {
                    cancellationTokenSource?.Cancel(); // Cancel sync
                }
            }
        }

        private async void btnSync_Click(object sender, EventArgs e)
        {
            if (!StaticVar.CheckInternetConnection())
            {
                MessageBox.Show("Internet connection required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (isSyncInProgress)
            {
                MessageBox.Show("Sync is already in progress.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                btnSync.Enabled = false;
                btnSync.Text = "Syncing...";
                isSyncInProgress = true;
                cancellationTokenSource = new CancellationTokenSource();

                ApiHelper apiHelper = new ApiHelper();

                (bool success, string message) = await Task.Run(async () =>
                {
                    return await apiHelper.SyncPlayerData(null);
                }, cancellationTokenSource.Token);
                if (success)
                {
                    MessageBox.Show("Players data synced successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadPlayerData(); // Refresh grid
                    OnSyncCompleted?.Invoke();
                }
                else
                {
                    MessageBox.Show("Failed to sync players data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Sync was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSync.Enabled = true;
                btnSync.Text = "Sync With Live";
                isSyncInProgress = false;
                cancellationTokenSource?.Dispose();
            }
        }


        private void frmPlayer_Load(object sender, EventArgs e)
        {
            LoadPlayerData();
        }

        private void LoadPlayerData()
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Users"); cmd.CommandType = CommandType.Text;
            DatabaseHelper dbHelper = new DatabaseHelper(); DataTable dt = dbHelper.Read(cmd);
            playerDgv.AutoGenerateColumns = false;
            playerDgv.DataSource = dt;
        }
    }
}
