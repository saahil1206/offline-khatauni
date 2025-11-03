using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OfflineOps
{
    public partial class frmMain : Form
    {
        private Timer bazarTimer;
        private ApiHelper syncManager = new ApiHelper();
        private bool isWaitingForSync = false;
        private System.Windows.Forms.Timer syncTimer;
        private bool timerInUse = false;

        public frmMain()
        {
            InitializeComponent();
            this.Load += frmMain_Load;
            deActivateToolStripMenuItem.Click += deActivateToolStripMenuItem_Click;
            bazarsToolStripMenuItem.Click += bazarsToolStripMenuItem_Click;
            playersToolStripMenuItem.Click += playersToolStripMenuItem_Click;
            shortcutsToolStripMenuItem.Click += shortcutsToolStripMenuItem_Click;
            txtPlayer.SelectedIndexChanged += txtPlayer_SelectedIndexChanged;
            txtGame.SelectedIndexChanged += txtGame_SelectedIndexChanged;
            txtDgv.CellContentClick += txtDgv_CellContentClick;
            txtDgv.CellFormatting += txtDgv_CellFormatting;
            btnSubmit.Click += btnSubmit_Click;
            btnHistory.Click += btnHistory_Click;
            this.FormClosing += frmMain_FormClosing;
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 3000; syncTimer.Tick += syncTimer_Tick;
            syncTimer.Start();
        }


        private void syncTimer_Tick(object sender, EventArgs e)
        {
            if (timerInUse) return;

            timerInUse = true;

            if (StaticVar.CheckInternetConnection() && !syncManager.IsSyncRunning)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                            lbStrip.Visible = true; pgStrip.Visible = true;
                            pgStrip.Style = ProgressBarStyle.Marquee;
                            lbStrip.Text = "Processing to server...";
                        }));

                        var (success, message, userIds) = await syncManager.SyncLoad();
                        if (success)
                        {

                            this.Invoke(new Action(() =>
                            {
                                ComboItem selectedItem = txtPlayer.SelectedItem as ComboItem; long user_id = 0;
                                ComboItem selectedBazar = txtGame.SelectedItem as ComboItem; long game_id = 0;
                                if (selectedBazar != null) { long.TryParse(selectedBazar.Value.ToString(), out game_id); }
                                if (selectedItem != null && userIds != null)
                                {
                                    long.TryParse(selectedItem.Value.ToString(), out user_id);
                                    foreach (var id in userIds)
                                    {
                                        if (user_id == id) { LoadBalance(user_id); LoadHistory(user_id, game_id); break; }
                                    }
                                }
                            }));
                        }
                        else
                        {
                            this.Invoke(new Action(() =>
                            {
                                pgStrip.Style = ProgressBarStyle.Continuous;
                                lbStrip.Text = "Transaction Failed";
                            }));
                        }
                    }
                    catch { }
                    finally
                    {
                        timerInUse = false;
                        this.Invoke(new Action(() =>
                        {
                            lbStrip.Visible = false;
                            pgStrip.Visible = false;
                        }));
                    }
                });
            }
            else
            {
                timerInUse = false;
            }
        }


        private async void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            syncTimer?.Stop();

            // If sync is running and we're not already waiting
            if (syncManager.IsSyncRunning && !isWaitingForSync)
            {
                // Cancel the close event
                e.Cancel = true;
                isWaitingForSync = true;

                // Show message that user must wait
                MessageBox.Show(
                    "Sync is currently in progress.\n\n" +
                    "Please wait for the sync to complete before closing.\n\n" +
                    "The form will close automatically when sync is finished.",
                    "Please Wait - Sync in Progress",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Disable the entire form to prevent other actions
                this.Enabled = false;

                // Update title to show waiting state
                string originalTitle = this.Text;
                this.Text = "⏳ Waiting for sync to complete...";

                // Wait for sync to complete
                while (syncManager.IsSyncRunning)
                {
                    await Task.Delay(100);
                    Application.DoEvents(); // Keep UI responsive
                }
                // Close the application
                Application.Exit();
            }
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

            // Ctrl + T
            if (keyData == (Keys.Control | Keys.T))
            {
                frmHistory frmHistory = new frmHistory();
                frmHistory.ShowDialog();
                return true;
            }

            // Ctrl + Enter
            if (keyData == (Keys.Control | Keys.Enter))
            {
                btnSubmit_Click(btnSubmit, EventArgs.Empty);
                return true;
            }

            // Ctrl + S
            if (keyData == (Keys.Control | Keys.S))
            {
                OpenShortcutsWindow();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void playersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPlayerWindow();
        }

        private void bazarsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenBazarWindow();
        }

        private void shortcutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenShortcutsWindow();
        }

        private void txtDgv_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && txtDgv.Columns[e.ColumnIndex].Name == "btnCancel")
            {
                if (syncManager.IsSyncRunning && !isWaitingForSync)
                {
                    MessageBox.Show("Sync is currently in progress", "Please Wait - Sync in Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var row = txtDgv.Rows[e.RowIndex];

                string serverFlag = txtDgv.Columns.Contains("server_flag") ? row.Cells["server_flag"].Value?.ToString() ?? "False" : "False";

                string cancelStatus = txtDgv.Columns.Contains("cancel_status") ? row.Cells["cancel_status"].Value?.ToString() ?? "False" : "False";

                if (serverFlag == "True")
                {
                    return;
                }

                if (cancelStatus == "True")
                {
                    return;
                }

                string id = row.Cells["id"].Value?.ToString();
                string betId = row.Cells["bet_id"].Value?.ToString();

                var confirm = MessageBox.Show($"Do you really want to cancel this record ?", "Confirm Cancel", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    CancelRecord(id, betId);
                }
            }
        }

        private void txtDgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {

            if (e.RowIndex < 0) return;

            bool hasServerFlag = txtDgv.Columns.Contains("server_flag");
            bool hasCancelStatus = txtDgv.Columns.Contains("cancel_status");

            string flag = hasServerFlag ? txtDgv.Rows[e.RowIndex].Cells["server_flag"].Value?.ToString() ?? "False" : "False";
            string status = hasCancelStatus ? txtDgv.Rows[e.RowIndex].Cells["cancel_status"].Value?.ToString() ?? "False" : "False";

            if (flag == "True")
            {
                txtDgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
            }
            else if (status == "True")
            {
                txtDgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.IndianRed;
            }
            else
            {
                txtDgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
            }

            if (txtDgv.Columns[e.ColumnIndex].Name == "btnCancel")
            {
                if (flag == "True")
                {
                    e.Value = "";
                    e.CellStyle.BackColor = Color.White;
                }
                else if (status == "True")
                {
                    e.Value = "Cancelled";
                    e.CellStyle.BackColor = Color.DarkSlateGray;
                }
                else
                {
                    e.Value = "Cancel";
                    e.CellStyle.BackColor = Color.LightCoral;
                }

                e.FormattingApplied = true;
            }
        }


        private void OpenBazarWindow()
        {
            frmBazar frm = new frmBazar();
            frm.OnSyncCompleted += () =>
            {
              
                LoadGames();
            };
            frm.ShowDialog();
        }

        private void OpenPlayerWindow()
        {
            frmPlayer frm = new frmPlayer();
            frm.OnSyncCompleted += () =>
            {
                ComboItem selectedItem = txtGame.SelectedItem as ComboItem;
                long gameId = 0;
                if (selectedItem != null) { gameId = Convert.ToInt64(selectedItem.Value); }
                LoadPlayers(gameId);
            };
            frm.ShowDialog();
        }

        private void OpenShortcutsWindow()
        {
            frmShortcuts frm = new frmShortcuts();
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
            LoadGames();
            LoadHistory(0, 0);
            txtBazarTime.Text = "00h 00m 00s";
            this.ActiveControl = null;
        }

        private void LoadPlayers(long gameId)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT u.id, u.username FROM user_games g INNER JOIN users u ON g.user_id = u.id WHERE g.game_id = @game_id"); cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("@game_id",gameId);
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
            ComboItem selectedBazar = txtGame.SelectedItem as ComboItem; long game_id = 0;
            if (selectedBazar != null) { long.TryParse(selectedBazar.Value.ToString(), out game_id); }
            LoadBalance(user_id);
            LoadHistory(user_id, game_id);
        }

        public void LoadBalance(long user_id)
        {
            tblBalance.Controls.Clear();
            tblBalance.RowStyles.Clear();
            tblBalance.ColumnStyles.Clear();

            txtBal.Text = "0000000.00";
            txtPL.Text = "0000000.00";
            txtCr.Text = "0000000.00";

            var stats = new List<(string title, decimal total, decimal exposure, bool highlight)>
                {
                    ("Aakda", 0, 0, false),
                    ("Pana", 0, 0, false),
                    ("Group Pana", 0, 0, false),
                    ("Jodi", 0, 0, false),
                    ("Total", 0, 0, false),
                    ("Balance", 0, 0, true),
                    ("Not Uploaded", 0, 0, true),
                };

            if (user_id > 0)
            {
                try
                {
                    string query = @"
                      SELECT 
                            IFNULL(aakda_total, 0) AS aakda_total,
                            IFNULL(aakda_exposure, 0) AS aakda_exposure,
                            IFNULL(pana_total, 0) AS pana_total,
                            IFNULL(pana_exposure, 0) AS pana_exposure,
                            IFNULL(group_pana_total, 0) AS group_pana_total,
                            IFNULL(group_pana_exposure, 0) AS group_pana_exposure,
                            IFNULL(jodi_total, 0) AS jodi_total,
                            IFNULL(jodi_exposure, 0) AS jodi_exposure,

                            IFNULL(balance, 0) AS balance,
                            IFNULL(opening_credit, 0) AS opening_credit,
                            IFNULL(profit_loss, 0) AS profit_loss,

                            (
                                SELECT IFNULL(SUM(total_amount), 0)
                                FROM group_trans
                                WHERE user_id = users.id
                                    AND server_flag = 0
                                    AND DATE(game_date) = @date  
                            )
                            +
                            (
                                SELECT IFNULL(SUM(amount), 0)
                                FROM single_digit
                                WHERE user_id = users.id
                                    AND server_flag = 0
                                    AND cancel_status = 0
                                    AND DATE(game_date) = @date  
                            ) AS upload_balance

                        FROM users 
                        WHERE id = @user_id;
                    ";

                    string date = StaticVar.getGameCurrDate().Date.ToString("yyyy-MM-dd");

                    SQLiteCommand cmd = new SQLiteCommand(query);
                    cmd.Parameters.AddWithValue("@user_id", user_id);
                    cmd.Parameters.AddWithValue("@date", date);

                    DatabaseHelper dbHelper = new DatabaseHelper();
                    DataTable dt = dbHelper.Read(cmd);

                    if (dt.Rows.Count > 0)
                    {
                        var row = dt.Rows[0];

                        stats = new List<(string title, decimal total, decimal exposure, bool highlight)>
                        {
                            ("Aakda", (Convert.ToDecimal(row["aakda_total"])), (Convert.ToDecimal(row["aakda_exposure"])), false),
                            ("Pana", (Convert.ToDecimal(row["pana_total"]) ), (Convert.ToDecimal(row["pana_exposure"])), false),
                            ("Group Pana", Convert.ToDecimal(row["group_pana_total"]), Convert.ToDecimal(row["group_pana_exposure"]), false),
                            ("Jodi", (Convert.ToDecimal(row["jodi_total"])), (Convert.ToDecimal(row["jodi_exposure"])), false),
                        };

                        decimal totalSum = stats.Sum(s => s.total);
                        stats.Add(("Total", totalSum, 0, false));

                        stats.Add(("Balance", (Convert.ToDecimal(row["balance"])), 0, true));

                        stats.Add(("Not Uploaded", (Convert.ToDecimal(row["upload_balance"])), 0, true));

                        txtBal.Text = StaticVar.Format2(row["balance"], txtBal);
                        txtPL.Text = StaticVar.Format2(row["profit_loss"], txtPL);
                        txtCr.Text = StaticVar.Format2(row["opening_credit"], txtCr);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading balance: " + ex.Message);
                }
            }

            int columns = 7;
            int rows = (int)Math.Ceiling(stats.Count / (double)columns);

            tblBalance.ColumnCount = columns;
            tblBalance.RowCount = rows;

            for (int i = 0; i < columns; i++)
                tblBalance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));

            for (int i = 0; i < rows; i++)
                tblBalance.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (index >= stats.Count) break;
                    var stat = stats[index++];

                    decimal exposureToShow = stat.exposure;
                    bool showExposure = stat.title != "Total" && stat.title != "Balance" && stat.title != "Not Uploaded";

                    Panel card = CreateStatCard(stat.title, stat.total, exposureToShow, stat.highlight, showExposure);
                    tblBalance.Controls.Add(card, c, r);
                }
            }

        }

        private void LoadGames()
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT id, bazar_name FROM bazar"); cmd.CommandType = CommandType.Text;
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            txtGame.Items.Clear(); txtGame.Items.Add(new ComboItem("-- Select Game --", 0));
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                txtGame.Items.Add(new ComboItem(dt.Rows[i]["bazar_name"].ToString(), dt.Rows[i]["id"].ToString()));
            }
            txtGame.SelectedIndex = 0;
        }

        private Panel CreateStatCard(string title, decimal total, decimal exposure, bool highlight = false, bool showExposure = true)
        {
            Panel card = new Panel();
            card.Width = 150;
            card.Height = 150;
            card.Margin = new Padding(10);
            card.BackColor = title.Trim().Equals("Not Uploaded", StringComparison.OrdinalIgnoreCase)
                                ? Color.FromArgb(255, 212, 112)
                                : highlight
                                    ? Color.FromArgb(198, 224, 255)
                                    : Color.FromArgb(240, 240, 240);
            card.ForeColor = Color.Black;
            card.BorderStyle = BorderStyle.None;
            card.Padding = new Padding(5);

            Label lblTotal = new Label();
            lblTotal.Text = total.ToString("F2");
            lblTotal.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            lblTotal.AutoSize = true;
            lblTotal.TextAlign = ContentAlignment.MiddleCenter;
            lblTotal.Dock = DockStyle.Top;
            lblTotal.Height = 40;

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            lblTitle.ForeColor = Color.Black;
            lblTitle.AutoSize = true;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 25;

            if (showExposure)
            {
                Label lblExposure = new Label();
                lblExposure.Text = exposure.ToString("F2");
                lblExposure.Font = new Font("Segoe UI", 10, FontStyle.Regular);
                lblExposure.ForeColor = Color.Gray;
                lblExposure.AutoSize = true;
                lblExposure.TextAlign = ContentAlignment.MiddleCenter;
                lblExposure.Dock = DockStyle.Top;
                lblExposure.Height = 25;
                card.Controls.Add(lblExposure);
            }

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblTotal);

            return card;
        }

        private void txtGame_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bazarTimer?.Enabled == true) { bazarTimer.Stop(); }
            if (txtSession.Items.Count > 0) { txtSession.SelectedIndex = 0; }
            ComboItem selectedUser = txtPlayer.SelectedItem as ComboItem; long user_id = 0;
            if (selectedUser != null) { long.TryParse(selectedUser.Value.ToString(), out user_id); }
            ComboItem selectedBazar = txtGame.SelectedItem as ComboItem; long game_id = 0;
            if (selectedBazar != null) { long.TryParse(selectedBazar.Value.ToString(), out game_id); }
            LoadPlayers(game_id);
            LoadSession(game_id);
            LoadHistory(user_id, game_id);
        }

        private void LoadSession(long game_id)
        {
            txtBazarTime.Text = "00h 00m 00s";
            SQLiteCommand cmd = new SQLiteCommand(
                "SELECT id, open_time, open_start_time, close_time, close_start_time FROM bazar WHERE id = @bazar_id"
            );
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("@bazar_id", game_id);

            DatabaseHelper databaseHelper = new DatabaseHelper();
            DataTable dt = databaseHelper.Read(cmd);

            if (dt.Rows.Count == 0)
                return;

            var row = dt.Rows[0];

            DateTime gameDate = StaticVar.getGameCurrDate();

            DateTime openStart = DateTime.Parse(row["open_start_time"].ToString());
            DateTime openEnd = DateTime.Parse(row["open_time"].ToString());
            DateTime closeStart = DateTime.Parse(row["close_start_time"].ToString());
            DateTime closeEnd = DateTime.Parse(row["close_time"].ToString());

            openStart = new DateTime(gameDate.Year, gameDate.Month, gameDate.Day, openStart.Hour, openStart.Minute, openStart.Second);
            openEnd = new DateTime(gameDate.Year, gameDate.Month, gameDate.Day, openEnd.Hour, openEnd.Minute, openEnd.Second);
            closeStart = new DateTime(gameDate.Year, gameDate.Month, gameDate.Day, closeStart.Hour, closeStart.Minute, closeStart.Second);
            closeEnd = new DateTime(gameDate.Year, gameDate.Month, gameDate.Day, closeEnd.Hour, closeEnd.Minute, closeEnd.Second);

            DateTime now = DateTime.Now;

            txtSession.Items.Clear();
            txtSession.Items.Add(new ComboItem("-- Session --", 0));
            txtSession.Items.Add(new ComboItem("Open", 1));
            txtSession.Items.Add(new ComboItem("Close", 2));

            if (now >= openStart && now <= openEnd)
            {
                txtSession.SelectedIndex = 1;
            }
            else if (now >= closeStart && now <= closeEnd)
            {
                txtSession.SelectedIndex = 2;
            }
            else
            {
                txtSession.SelectedIndex = 0;
            }
            StartBazarCountdown(openStart, openEnd, closeStart, closeEnd);
        }

        private void StartBazarCountdown(DateTime openStart, DateTime openEnd, DateTime closeStart, DateTime closeEnd)
        {
            if (bazarTimer != null)
                bazarTimer.Stop();

            bazarTimer = new Timer();
            bazarTimer.Interval = 1000; // 1 second
            bazarTimer.Tick += (s, e) =>
            {
                DateTime now = DateTime.Now;
                TimeSpan remaining;

                if (now >= openStart && now <= openEnd)
                {
                    remaining = openEnd - now;
                    txtBazarTime.Text = $"{remaining.Hours:D2}h {remaining.Minutes:D2}m {remaining.Seconds:D2}s";
                }
                else if (now >= closeStart && now <= closeEnd)
                {
                    remaining = closeEnd - now;
                    txtBazarTime.Text = $"{remaining.Hours:D2}h {remaining.Minutes:D2}m {remaining.Seconds:D2}s";
                }
                else
                {
                    txtBazarTime.Text = "Closed";
                    bazarTimer.Stop();
                }
            };

            bazarTimer.Start();
        }


        private void btnSubmit_Click(object sender, EventArgs e)
        {
            btnSubmit.Text = "Submiting...";
            btnSubmit.Enabled = false;

            string input = txtConsole.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Console is empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSubmit.Text = "Submit";
                btnSubmit.Enabled = true;
                return;
            }

            var entries = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => s.Trim())
                               .ToList();

            if (!entries.Any())
            {
                MessageBox.Show("No valid entries.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSubmit.Text = "Submit";
                btnSubmit.Enabled = true;
                return;
            }

            ComboItem selectedPlayer = txtPlayer.SelectedItem as ComboItem;
            ComboItem selectedGame = txtGame.SelectedItem as ComboItem;
            ComboItem selectedSession = txtSession.SelectedItem as ComboItem;

            int.TryParse(selectedPlayer?.Value?.ToString(), out int playerId);
            int.TryParse(selectedGame?.Value?.ToString(), out int gameId);
            int.TryParse(selectedSession?.Value?.ToString(), out int sessionId);


            if (selectedPlayer == null || playerId == 0
                || selectedGame == null || gameId == 0
                || selectedSession == null || sessionId == 0)
            {
                MessageBox.Show("Please select Player, Game, and Session.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSubmit.Text = "Submit";
                btnSubmit.Enabled = true;
                return;
            }

            GameProcessor gp = new GameProcessor();

            var response = gp.ProcessGameInput(input, 0, 0, 0);

            string msg_string = Regex.Replace(input, "[a-zA-Z]", "").Trim();

            string[] amt_nu = gp.MultiExplode(new[] { ':', '=', '*', '-', '+' }, msg_string);

            string group_id = Guid.NewGuid().ToString();
            string bet_id = Guid.NewGuid().ToString();

            string gameName = gp.group_game_name;
            string gameTestName = gp.group_test_name;

            Dictionary<string, double> single_aakda = response["single_aakda"] as Dictionary<string, double>;
            List<string> single_pana = response["single_pana"] as List<string>;
            List<string> single_jodi = response["single_jodi"] as List<string>;
            List<string> group_pana = response["group_pana"] as List<string>;

            double grp_tlt_amt = Convert.ToDouble(response["grp_tlt_amt"]);
            double aakda_tlt_amt = Convert.ToDouble(response["aakda_tlt_amt"]);
            double pana_tlt_amt = Convert.ToDouble(response["pana_tlt_amt"]);
            double jodi_tlt_amt = Convert.ToDouble(response["jodi_tlt_amt"]);

            int total_numbers = Convert.ToInt32(response["total_numbers"]);
            string numbers_new = response["numbers_new"]?.ToString();

            if (string.IsNullOrEmpty(numbers_new))
            {
                MessageBox.Show($"Wrong Format", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSubmit.Text = "Submit";
                btnSubmit.Enabled = true;
                return;
            }

            double totalDeductedAmount = aakda_tlt_amt + pana_tlt_amt + jodi_tlt_amt + grp_tlt_amt;

            using (var db = new DatabaseHelper())
            {
                db.BeginTransaction();
                try
                {

                    //var usercmd = new SQLiteCommand(@"SELECT balance FROM users WHERE id = @id");
                    //usercmd.Parameters.AddWithValue("@id", playerId); DataTable dt = db.Read(usercmd);

                    //if (dt.Rows.Count > 0)
                    //{
                    //    double balance = 0;
                    //    double.TryParse(dt.Rows[0]["balance"].ToString(), out balance);

                    //    //if (balance < totalDeductedAmount)
                    //    //{
                    //    //    MessageBox.Show($"Insufficient Balance", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //    //    btnSubmit.Text = "Submit";
                    //    //    btnSubmit.Enabled = true;
                    //    //    db.Rollback();
                    //    //    return;
                    //    //}
                    //}
                    //else
                    //{
                    //    MessageBox.Show($"No User Found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //    btnSubmit.Text = "Submit";
                    //    btnSubmit.Enabled = true;
                    //    db.Rollback();
                    //    return;
                    //}

                    if (single_aakda.Count > 0)
                    {
                        var cmd = new SQLiteCommand(@"
                    INSERT INTO single_digit 
                    (id,bet_id,user_id,bazar_id,bazar_cat,single0,single1,single2,single3,single4,single5,single6,single7,single8,single9,amount,server_flag,game_date,upload_date,created_date)
                    VALUES (@id,@bet_id,@user_id,@bazar_id,@bazar_cat,@single0,@single1,@single2,@single3,@single4,@single5,@single6,@single7,@single8,@single9,@amount,0,@game_date,@upload_date,@created_date)");

                        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmd.Parameters.AddWithValue("@bet_id", bet_id);
                        cmd.Parameters.AddWithValue("@user_id", playerId);
                        cmd.Parameters.AddWithValue("@bazar_id", gameId);
                        cmd.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");

                        for (int i = 0; i <= 9; i++)
                        {
                            string key = $"single{i}";
                            cmd.Parameters.AddWithValue($"@{key}",
                                single_aakda.ContainsKey(key) ? single_aakda[key] : 0);
                        }

                        cmd.Parameters.AddWithValue("@amount", aakda_tlt_amt);
                        cmd.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@upload_date", DBNull.Value);
                        cmd.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));

                        db.Update(cmd);
                    }


                    if (single_pana.Count > 0)
                    {
                        foreach (var item in single_pana)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                            {
                                var parts = item.Split('=');

                                string number = parts[0];
                                string amount = parts[1];

                                var cmd = new SQLiteCommand(@"
                            INSERT INTO pana (id,group_id,user_id,bazar_id,bazar_cat,pana,amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@group_id,@user_id,@bazar_id,@bazar_cat,@pana,@amount,0,@game_date,@upload_date,@created_date)");
                                cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                                cmd.Parameters.AddWithValue("@group_id", group_id);
                                cmd.Parameters.AddWithValue("@user_id", playerId);
                                cmd.Parameters.AddWithValue("@bazar_id", gameId);
                                cmd.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                                cmd.Parameters.AddWithValue("@pana", number);
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@upload_date", DBNull.Value);
                                cmd.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                                db.Update(cmd);
                            }
                        }

                        string no = string.Join(",", single_pana).Replace("=", "=>");

                        var cmdt = new SQLiteCommand(@"
                            INSERT INTO group_trans (id,bet_id,user_id,bazar_id,bazar_cat,game_name,aakda_no,pana_no,amount,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@bet_id,@user_id,@bazar_id,@bazar_cat,@game_name,@aakda_no,@pana_no,@amount,@total_amount,0,@game_date,@upload_date,@created_date)");
                        cmdt.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmdt.Parameters.AddWithValue("@bet_id", bet_id);
                        cmdt.Parameters.AddWithValue("@user_id", playerId);
                        cmdt.Parameters.AddWithValue("@bazar_id", gameId);
                        cmdt.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                        cmdt.Parameters.AddWithValue("@game_name", "pana220");
                        cmdt.Parameters.AddWithValue("@aakda_no", no);
                        cmdt.Parameters.AddWithValue("@pana_no", no);
                        cmdt.Parameters.AddWithValue("@amount", 0);
                        cmdt.Parameters.AddWithValue("@total_amount", pana_tlt_amt);
                        cmdt.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                        cmdt.Parameters.AddWithValue("@upload_date", DBNull.Value);
                        cmdt.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                        db.Update(cmdt);
                    }

                    if (single_jodi.Count > 0)
                    {
                        foreach (var item in single_jodi)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                            {
                                var parts = item.Split('=');

                                string number = parts[0];
                                string amount = parts[1];

                                var cmd = new SQLiteCommand(@"
                            INSERT INTO jodi (id,group_id,user_id,bazar_id,bazar_cat,jodi,amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@group_id,@user_id,@bazar_id,@bazar_cat,@jodi,@amount,0,@game_date,@upload_date,@created_date)");
                                cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                                cmd.Parameters.AddWithValue("@group_id", group_id);
                                cmd.Parameters.AddWithValue("@user_id", playerId);
                                cmd.Parameters.AddWithValue("@bazar_id", gameId);
                                cmd.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                                cmd.Parameters.AddWithValue("@jodi", number);
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@upload_date", DBNull.Value);
                                cmd.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                                db.Update(cmd);
                            }
                        }

                        string no2 = string.Join(",", single_jodi).Replace("=", "=>");

                        var cmdt2 = new SQLiteCommand(@"
                            INSERT INTO group_trans (id,bet_id,user_id,bazar_id,bazar_cat,game_name,aakda_no,pana_no,amount,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@bet_id,@user_id,@bazar_id,@bazar_cat,@game_name,@aakda_no,@pana_no,@amount,@total_amount,0,@game_date,@upload_date,@created_date)");
                        cmdt2.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmdt2.Parameters.AddWithValue("@bet_id", bet_id);
                        cmdt2.Parameters.AddWithValue("@user_id", playerId);
                        cmdt2.Parameters.AddWithValue("@bazar_id", gameId);
                        cmdt2.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                        cmdt2.Parameters.AddWithValue("@game_name", "all jodi");
                        cmdt2.Parameters.AddWithValue("@aakda_no", no2);
                        cmdt2.Parameters.AddWithValue("@pana_no", no2);
                        cmdt2.Parameters.AddWithValue("@amount", 0);
                        cmdt2.Parameters.AddWithValue("@total_amount", jodi_tlt_amt);
                        cmdt2.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                        cmdt2.Parameters.AddWithValue("@upload_date", DBNull.Value);
                        cmdt2.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                        db.Update(cmdt2);
                    }

                    if (group_pana.Count > 0)
                    {
                        foreach (var item in group_pana)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                            {
                                var cmdp = new SQLiteCommand(@"
                            INSERT INTO pana (id,group_id,user_id,bazar_id,bazar_cat,pana,amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@group_id,@user_id,@bazar_id,@bazar_cat,@pana,@amount,0,@game_date,@upload_date,@created_date)");
                                cmdp.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                                cmdp.Parameters.AddWithValue("@group_id", group_id);
                                cmdp.Parameters.AddWithValue("@user_id", playerId);
                                cmdp.Parameters.AddWithValue("@bazar_id", gameId);
                                cmdp.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                                cmdp.Parameters.AddWithValue("@pana", item);
                                cmdp.Parameters.AddWithValue("@amount", amt_nu[1]);
                                cmdp.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                                cmdp.Parameters.AddWithValue("@upload_date", DBNull.Value);
                                cmdp.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                                db.Update(cmdp);
                            }
                        }

                        var cmdt2 = new SQLiteCommand(@"
                            INSERT INTO group_trans (id,bet_id,user_id,bazar_id,bazar_cat,game_name,game_test_name,aakda_no,pana_no,amount,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@bet_id,@user_id,@bazar_id,@bazar_cat,@game_name,@game_test_name,@aakda_no,@pana_no,@amount,@total_amount,0,@game_date,@upload_date,@created_date)");
                        cmdt2.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmdt2.Parameters.AddWithValue("@bet_id", bet_id);
                        cmdt2.Parameters.AddWithValue("@user_id", playerId);
                        cmdt2.Parameters.AddWithValue("@bazar_id", gameId);
                        cmdt2.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                        cmdt2.Parameters.AddWithValue("@game_name", gameName);
                        cmdt2.Parameters.AddWithValue("@game_test_name", gameTestName);
                        cmdt2.Parameters.AddWithValue("@aakda_no", amt_nu[0]);
                        cmdt2.Parameters.AddWithValue("@pana_no", string.Join(",", group_pana));
                        cmdt2.Parameters.AddWithValue("@amount", amt_nu[1]);
                        cmdt2.Parameters.AddWithValue("@total_amount", grp_tlt_amt);
                        cmdt2.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                        cmdt2.Parameters.AddWithValue("@upload_date", DBNull.Value);
                        cmdt2.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                        db.Update(cmdt2);
                    }


                    var cmdt3 = new SQLiteCommand(@"
                            INSERT INTO bet_request (id,user_id,bazar_id,bazar_cat,bet_str,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@user_id,@bazar_id,@bazar_cat,@bet_str,@total_amount,0,@game_date,@upload_date,@created_date)");
                    cmdt3.Parameters.AddWithValue("@id", bet_id);
                    cmdt3.Parameters.AddWithValue("@user_id", playerId);
                    cmdt3.Parameters.AddWithValue("@bazar_id", gameId);
                    cmdt3.Parameters.AddWithValue("@bazar_cat", sessionId == 2 ? "close" : "open");
                    cmdt3.Parameters.AddWithValue("@bet_str", numbers_new);
                    cmdt3.Parameters.AddWithValue("@total_amount", totalDeductedAmount);
                    cmdt3.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                    cmdt3.Parameters.AddWithValue("@upload_date", DBNull.Value);
                    cmdt3.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    db.Update(cmdt3);

                    var updateBalanceCmd = new SQLiteCommand(@"
                    UPDATE users 
                    SET balance = IFNULL(balance,0) - @deduct 
                    WHERE id = @user_id");
                    updateBalanceCmd.Parameters.AddWithValue("@deduct", totalDeductedAmount);
                    updateBalanceCmd.Parameters.AddWithValue("@user_id", playerId);
                    db.Update(updateBalanceCmd);
                    db.Commit();


                    MessageBox.Show("Transaction Successful.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtConsole.Text = "";
                    btnSubmit.Text = "Submit";
                    btnSubmit.Enabled = true;

                    syncTimer_Tick(null, null);
                    LoadHistory(playerId, gameId);
                    LoadBalance(playerId);
                }
                catch (Exception ex)
                {
                    db.Rollback();
                    MessageBox.Show($"Transaction failed\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSubmit.Text = "Submit";
                    btnSubmit.Enabled = true;
                    return;
                }
            }
        }

        private void LoadHistory(long userId, long bazarId)
        {
            DatabaseHelper dbHelper = new DatabaseHelper();
            DataTable dt = new DataTable();

            if (bazarId > 0)
            {
                string query = "";

                if (userId > 0)
                {
                    query = @"
                         SELECT 
                            tr.id,
	                        tr.bet_id,
                            b.bazar_name,
                            tr.bazar_id AS bazar,
                            tr.bazar_cat AS type,
                            tr.single0,
                            tr.single1,
                            tr.single2,
                            tr.single3,
                            tr.single4,
                            tr.single5,
                            tr.single6,
                            tr.single7,
                            tr.single8,
                            tr.single9,
                            tr.game_date AS date,
                            tr.game_name,
                            tr.aakda_no,
                            tr.pana_no,
                            tr.amount,
                            tr.cancel_status,
                            tr.total_amount,
                            tr.created_date AS createddt,
                            tr.server_flag,
                            u.username
                            
                        FROM (
                            SELECT
                                id,
		                        bet_id,
                                bazar_id,
                                bazar_cat,
                                0 AS single0, 0 AS single1, 0 AS single2, 0 AS single3, 0 AS single4,
                                0 AS single5, 0 AS single6, 0 AS single7, 0 AS single8, 0 AS single9,
                                game_date,
                                game_name,
                                CAST(aakda_no AS TEXT) AS aakda_no,
                                CAST(pana_no AS TEXT) AS pana_no,
                                amount,
                                cancel_status,
                                total_amount,
                                created_date,
                                server_flag,
                                user_id
                            FROM group_trans
                            WHERE 
                                bazar_id = @bazar_id
                                AND user_id = @userId
                                AND DATE(game_date) = @gameDate

                            UNION

                            SELECT
                                id,
		                        bet_id,
                                bazar_id,
                                bazar_cat,
                                single0, single1, single2, single3, single4,
                                single5, single6, single7, single8, single9,
                                game_date,
                                'Aakda' AS game_name,
                                '' AS aakda_no,
                                '' AS pana_no,
                                0 AS amount,
                                cancel_status,
                                amount AS total_amount,
                                created_date,
                                server_flag,
                                user_id
                            FROM single_digit
                            WHERE 
                                bazar_id = @bazar_id
                                AND user_id = @userId
                                AND DATE(game_date) = @gameDate
                        ) AS tr
                        INNER JOIN bazar AS b ON b.id = tr.bazar_id
                        INNER JOIN users AS u ON u.id = tr.user_id
                        ORDER BY tr.created_date DESC;
                ";
                }
                else
                {
                    query = @"
                         SELECT 
                            tr.id,
		                    tr.bet_id,
                            b.bazar_name,
                            tr.bazar_id AS bazar,
                            tr.bazar_cat AS type,
                            tr.single0,
                            tr.single1,
                            tr.single2,
                            tr.single3,
                            tr.single4,
                            tr.single5,
                            tr.single6,
                            tr.single7,
                            tr.single8,
                            tr.single9,
                            tr.game_date AS date,
                            tr.game_name,
                            tr.aakda_no,
                            tr.pana_no,
                            tr.amount,
                            tr.cancel_status,
                            tr.total_amount,
                            tr.created_date AS createddt,
                            tr.server_flag,
                            u.username
                        FROM (
                            SELECT
                                id,
		                        bet_id,
                                bazar_id,
                                bazar_cat,
                                0 AS single0, 0 AS single1, 0 AS single2, 0 AS single3, 0 AS single4,
                                0 AS single5, 0 AS single6, 0 AS single7, 0 AS single8, 0 AS single9,
                                game_date,
                                game_name,
                                CAST(aakda_no AS TEXT) AS aakda_no,
                                CAST(pana_no AS TEXT) AS pana_no,
                                amount,
                                cancel_status,
                                total_amount,
                                created_date,
                                server_flag,
                                user_id
                            FROM group_trans
                            WHERE 
                                bazar_id = @bazar_id
                                AND DATE(game_date) = @gameDate

                            UNION

                            SELECT
                                id,
		                        bet_id,
                                bazar_id,
                                bazar_cat,
                                single0, single1, single2, single3, single4,
                                single5, single6, single7, single8, single9,
                                game_date,
                                'Aakda' AS game_name,
                                '' AS aakda_no,
                                '' AS pana_no,
                                0 AS amount,
                                cancel_status,
                                amount AS total_amount,
                                created_date,
                                server_flag,
                                user_id
                            FROM single_digit
                            WHERE 
                                bazar_id = @bazar_id
                                AND DATE(game_date) = @gameDate
                        ) AS tr
                        INNER JOIN bazar AS b ON b.id = tr.bazar_id
                        INNER JOIN users AS u ON u.id = tr.user_id
                        ORDER BY tr.created_date DESC;
                ";
                }


                SQLiteCommand cmd = new SQLiteCommand(query);
                cmd.Parameters.AddWithValue("@bazar_id", bazarId);
                if (userId > 0) { cmd.Parameters.AddWithValue("@userId", userId); }
                cmd.Parameters.AddWithValue("@gameDate", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                dt = dbHelper.Read(cmd);

                if (!dt.Columns.Contains("bet_request"))
                {
                    dt.Columns.Add("bet_request", typeof(string));
                }

                foreach (DataRow row in dt.Rows)
                {
                    row["createddt"] = StaticVar.ConvertToCustomTime(row["createddt"]?.ToString());

                    string betReq = "";

                    if (row["game_name"]?.ToString() == "Aakda")
                    {
                        for (int i = 1; i <= 9; i++)
                            betReq += i + "-" + row[$"single{i}"] + ",";

                        betReq += "0-" + row["single0"];
                    }
                    else
                    {
                        betReq = row["pana_no"]?.ToString();
                    }

                    row["bet_request"] = betReq;
                }

                txtDgv.AutoGenerateColumns = false;
                txtDgv.DataSource = dt;

                if (!txtDgv.Columns.Contains("btnCancel"))
                {
                    DataGridViewButtonColumn btnCancel = new DataGridViewButtonColumn();
                    btnCancel.HeaderText = "Action";
                    btnCancel.Text = "Cancel";
                    btnCancel.Name = "btnCancel";
                    btnCancel.UseColumnTextForButtonValue = true;
                    txtDgv.Columns.Add(btnCancel);
                }
            }
            else
            {
                txtDgv.AutoGenerateColumns = false;
                txtDgv.DataSource = dt;
            }
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            frmHistory frmHistory = new frmHistory();
            frmHistory.ShowDialog();
        }

        private void CancelRecord(string id, string betId)
        {
            try
            {
                DatabaseHelper db = new DatabaseHelper();
                string query = $@"
                                UPDATE single_digit SET cancel_status = 1 WHERE id = @id;
                                UPDATE group_trans SET cancel_status = 1 WHERE id = @id;
                            ";

                SQLiteCommand cmd = new SQLiteCommand(query);
                cmd.Parameters.AddWithValue("@id", id);
                db.Update(cmd);

                MessageBox.Show($"Record cancelled successfully.",
                                "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ComboItem selectedUser = txtPlayer.SelectedItem as ComboItem; long user_id = 0;
                if (selectedUser != null) { long.TryParse(selectedUser.Value.ToString(), out user_id); }
                ComboItem selectedBazar = txtGame.SelectedItem as ComboItem; long game_id = 0;
                if (selectedBazar != null) { long.TryParse(selectedBazar.Value.ToString(), out game_id); }
                LoadBalance(user_id);
                LoadHistory(user_id, game_id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cancelling record: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


    }
}