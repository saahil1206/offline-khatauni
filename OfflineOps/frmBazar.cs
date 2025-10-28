using System;
using System.Data;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfflineOps
{
    public partial class frmBazar : Form
    {
        private bool isSyncInProgress = false;
        private CancellationTokenSource cancellationTokenSource;
        public event Action OnSyncCompleted;

        public frmBazar()
        {
            InitializeComponent();
            this.Load += frmBazar_Load;
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
                    return await apiHelper.SyncBazarData();
                }, cancellationTokenSource.Token);
                if (success)
                {
                    MessageBox.Show("Bazar data synced successfully!", "Success",
                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadBazarData(); // Refresh grid
                    OnSyncCompleted?.Invoke();
                }
                else
                {
                    MessageBox.Show("Failed to sync bazar data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void frmBazar_Load(object sender, EventArgs e)
        {
            LoadBazarData();
        }

        private void LoadBazarData()
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM bazar"); cmd.CommandType = CommandType.Text;
            DatabaseHelper dbHelper = new DatabaseHelper(); DataTable dt = dbHelper.Read(cmd);
            for (int i = 0; i < dt.Columns.Count; i++) { dt.Columns[i].ReadOnly = false; }
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                dt.Rows[i]["open_time"] = StaticVar.ConvertToAmPm(dt.Rows[i]["open_time"]?.ToString());
                dt.Rows[i]["close_time"] = StaticVar.ConvertToAmPm(dt.Rows[i]["close_time"]?.ToString());
                dt.Rows[i]["monday"] = (dt.Rows[i]["monday"]?.ToString() == "1" ? "YES" : "NO");
                dt.Rows[i]["tuesday"] = (dt.Rows[i]["tuesday"]?.ToString() == "1" ? "YES" : "NO");
                dt.Rows[i]["wednesday"] = (dt.Rows[i]["wednesday"]?.ToString() == "1" ? "YES" : "NO");
                dt.Rows[i]["Thursday"] = (dt.Rows[i]["Thursday"]?.ToString() == "1" ? "YES" : "NO");
                dt.Rows[i]["friday"] = (dt.Rows[i]["friday"]?.ToString() == "1" ? "YES" : "NO");
                dt.Rows[i]["saturday"] = (dt.Rows[i]["saturday"]?.ToString() == "1" ? "YES" : "NO");
                dt.Rows[i]["sunday"] = (dt.Rows[i]["sunday"]?.ToString() == "1" ? "YES" : "NO");
            }
            txtDgv.AutoGenerateColumns = false;
            txtDgv.DataSource = dt; 
        }
    }
}