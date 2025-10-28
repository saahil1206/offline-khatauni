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
            btnSubmit.Click += btnSubmit_Click;
            btnHistory.Click += btnHistory_Click;
            this.FormClosing += frmMain_FormClosing;
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 10000; syncTimer.Tick += syncTimer_Tick;
            syncTimer.Start();
        }

        private void syncTimer_Tick(object sender, EventArgs e)
        {
            if (timerInUse) { return; }
            timerInUse = true;
            if (StaticVar.CheckInternetConnection() && !syncManager.IsSyncRunning)
            {
                Task.Run(async () => { try { await syncManager.SyncLoad(); } catch { } });
            }
            timerInUse = false;
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

        private void OpenBazarWindow()
        {
            frmBazar frm = new frmBazar();
            frm.OnSyncCompleted += () =>
            {
                ComboItem selectedItem = txtPlayer.SelectedItem as ComboItem;
                long userId = 0;
                if (selectedItem != null) { userId = Convert.ToInt64(selectedItem.Value); }
                LoadGames(userId);
            };
            frm.ShowDialog();
        }

        private void OpenPlayerWindow()
        {
            frmPlayer frm = new frmPlayer();
            frm.OnSyncCompleted += () =>
            {
                LoadPlayers();
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
            LoadPlayers();
            LoadHistory();
            txtBazarTime.Text = "00h 00m 00s";
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
            tblBalance.Controls.Clear();
            tblBalance.RowStyles.Clear();
            tblBalance.ColumnStyles.Clear();

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
                            IFNULL(balance, 0) AS balance
                        FROM users 
                        WHERE id = @user_id AND sync_date = @date
                        LIMIT 1;
                    ";

                    string date = StaticVar.getGameCurrDate().Date.ToString("yyyy-MM-dd");

                    SQLiteCommand cmd = new SQLiteCommand(query);
                    cmd.Parameters.AddWithValue("@user_id", user_id);
                    cmd.Parameters.AddWithValue("@date", date);

                    SQLiteCommand cmd1 = new SQLiteCommand(@"
                        SELECT 
                            u.id AS user_id,
                            u.username,
                            u.contact,
                            IFNULL(sd.total_amount, 0) AS aakda_total,
                            IFNULL(pn.total_amount, 0) AS pana_total,
                            IFNULL(jd.total_amount, 0) AS jodi_total,
                            (
                                IFNULL(sd.total_amount, 0) +
                                IFNULL(pn.total_amount, 0) +
                                IFNULL(jd.total_amount, 0)
                            ) AS total_amount
                        FROM users u
                        LEFT JOIN (
                            SELECT user_id, SUM(amount) AS total_amount
                            FROM single_digit
                            WHERE server_flag = 0 AND upload_date IS NULL
                            GROUP BY user_id
                        ) sd ON sd.user_id = u.id
                        LEFT JOIN (
                            SELECT user_id, SUM(amount) AS total_amount
                            FROM pana
                            WHERE server_flag = 0 AND upload_date IS NULL
                            GROUP BY user_id
                        ) pn ON pn.user_id = u.id
                        LEFT JOIN (
                            SELECT user_id, SUM(amount) AS total_amount
                            FROM jodi
                            WHERE server_flag = 0 AND upload_date IS NULL
                            GROUP BY user_id
                        ) jd ON jd.user_id = u.id
                        WHERE u.id = @user_id;

                    ");

                    cmd1.Parameters.AddWithValue("@user_id", user_id);

                    DatabaseHelper dbHelper = new DatabaseHelper();
                    DataTable dt = dbHelper.Read(cmd);
                    DataTable dt1 = dbHelper.Read(cmd1);

                    if (dt.Rows.Count > 0)
                    {
                        var row = dt.Rows[0];

                        decimal aakdaTotal = 0, panaTotal = 0, jodiTotal = 0, balanceRemaining = 0;

                        if (dt1.Rows.Count > 0)
                        {
                            aakdaTotal = Convert.ToDecimal(dt1.Rows[0]["aakda_total"]);
                            panaTotal = Convert.ToDecimal(dt1.Rows[0]["pana_total"]);
                            jodiTotal = Convert.ToDecimal(dt1.Rows[0]["jodi_total"]);
                            balanceRemaining = Convert.ToDecimal(dt1.Rows[0]["total_amount"]);
                        }

                        stats = new List<(string title, decimal total, decimal exposure, bool highlight)>
                        {
                            ("Aakda", (Convert.ToDecimal(row["aakda_total"]) - aakdaTotal), (Convert.ToDecimal(row["aakda_exposure"]) - aakdaTotal), false),
                            ("Pana", (Convert.ToDecimal(row["pana_total"]) - panaTotal), (Convert.ToDecimal(row["pana_exposure"]) - panaTotal), false),
                            ("Group Pana", Convert.ToDecimal(row["group_pana_total"]), Convert.ToDecimal(row["group_pana_exposure"]), false),
                            ("Jodi", (Convert.ToDecimal(row["jodi_total"]) - jodiTotal), (Convert.ToDecimal(row["jodi_exposure"]) - jodiTotal), false),
                        };

                        decimal totalSum = stats.Sum(s => s.total);
                        stats.Add(("Total", totalSum, 0, false));

                        stats.Add(("Balance", (Convert.ToDecimal(row["balance"]) - balanceRemaining), 0, true));

                        stats.Add(("Not Uploaded", -balanceRemaining, 0, true));
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

        private Panel CreateStatCard(string title, decimal total, decimal exposure, bool highlight = false, bool showExposure = true)
        {
            Panel card = new Panel();
            card.Width = 120;
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
            lblTotal.Text = total.ToString("N0");
            lblTotal.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            lblTotal.AutoSize = false;
            lblTotal.TextAlign = ContentAlignment.MiddleCenter;
            lblTotal.Dock = DockStyle.Top;
            lblTotal.Height = 40;

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            lblTitle.ForeColor = Color.Black;
            lblTitle.AutoSize = false;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 25;

            if (showExposure)
            {
                Label lblExposure = new Label();
                lblExposure.Text = exposure.ToString("N0");
                lblExposure.Font = new Font("Segoe UI", 10, FontStyle.Regular);
                lblExposure.ForeColor = Color.Gray;
                lblExposure.AutoSize = false;
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
            ComboItem selectedItem = txtGame.SelectedItem as ComboItem; long game_id = 0;
            if (selectedItem != null) { long.TryParse(selectedItem.Value.ToString(), out game_id); }
            LoadSession(game_id);
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




            if (selectedPlayer == null || Convert.ToInt32(selectedPlayer.Value) == 0
                || selectedGame == null || Convert.ToInt32(selectedGame.Value) == 0
                || selectedSession == null || Convert.ToInt32(selectedSession.Value) == 0)
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

            using (var db = new DatabaseHelper())
            {
                db.BeginTransaction();
                try
                {

                    if (single_aakda.Count > 0)
                    {
                        var cmd = new SQLiteCommand(@"
                    INSERT INTO single_digit 
                    (id,user_id,bazar_id,bazar_cat,single0,single1,single2,single3,single4,single5,single6,single7,single8,single9,amount,server_flag,game_date,upload_date,created_date)
                    VALUES (@id,@user_id,@bazar_id,@bazar_cat,@single0,@single1,@single2,@single3,@single4,@single5,@single6,@single7,@single8,@single9,@amount,0,@game_date,@upload_date,@created_date)");

                        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmd.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                        cmd.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                        cmd.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");

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
                                cmd.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                                cmd.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                                cmd.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");
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
                            INSERT INTO group_trans (id,user_id,bazar_id,bazar_cat,game_name,aakda_no,pana_no,amount,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@user_id,@bazar_id,@bazar_cat,@game_name,@aakda_no,@pana_no,@amount,@total_amount,0,@game_date,@upload_date,@created_date)");
                        cmdt.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmdt.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                        cmdt.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                        cmdt.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");
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
                                cmd.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                                cmd.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                                cmd.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");
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
                            INSERT INTO group_trans (id,user_id,bazar_id,bazar_cat,game_name,aakda_no,pana_no,amount,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@user_id,@bazar_id,@bazar_cat,@game_name,@aakda_no,@pana_no,@amount,@total_amount,0,@game_date,@upload_date,@created_date)");
                        cmdt2.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmdt2.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                        cmdt2.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                        cmdt2.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");
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
                                cmdp.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                                cmdp.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                                cmdp.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");
                                cmdp.Parameters.AddWithValue("@pana", item);
                                cmdp.Parameters.AddWithValue("@amount", amt_nu[1]);
                                cmdp.Parameters.AddWithValue("@game_date", StaticVar.getGameCurrDate().ToString("yyyy-MM-dd"));
                                cmdp.Parameters.AddWithValue("@upload_date", DBNull.Value);
                                cmdp.Parameters.AddWithValue("@created_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss"));
                                db.Update(cmdp);
                            }
                        }

                        var cmdt2 = new SQLiteCommand(@"
                            INSERT INTO group_trans (id,user_id,bazar_id,bazar_cat,game_name,game_test_name,aakda_no,pana_no,amount,total_amount,server_flag,game_date,upload_date,created_date)
                            VALUES (@id,@user_id,@bazar_id,@bazar_cat,@game_name,@game_test_name,@aakda_no,@pana_no,@amount,@total_amount,0,@game_date,@upload_date,@created_date)");
                        cmdt2.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmdt2.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                        cmdt2.Parameters.AddWithValue("@bazar_id", Convert.ToInt32(selectedGame.Value));
                        cmdt2.Parameters.AddWithValue("@bazar_cat", Convert.ToInt32(selectedSession.Value) == 2 ? "close" : "open");
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

                    double totalDeductedAmount = aakda_tlt_amt + pana_tlt_amt + jodi_tlt_amt + grp_tlt_amt;

                    var updateBalanceCmd = new SQLiteCommand(@"
                    UPDATE users 
                    SET balance = IFNULL(balance,0) - @deduct 
                    WHERE id = @user_id");
                    updateBalanceCmd.Parameters.AddWithValue("@deduct", totalDeductedAmount);
                    updateBalanceCmd.Parameters.AddWithValue("@user_id", Convert.ToInt32(selectedPlayer.Value));
                    db.Update(updateBalanceCmd);
                    db.Commit();


                    MessageBox.Show("Transaction Successful.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtConsole.Text = "";
                    btnSubmit.Text = "Submit";
                    btnSubmit.Enabled = true;

                    syncTimer_Tick(null, null);

                    LoadHistory();
                    LoadBalance(Convert.ToInt32(selectedPlayer.Value));
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

        private void LoadHistory()
        {
            DatabaseHelper dbHelper = new DatabaseHelper();

            string query = @"
                         SELECT 
                            s.id,
                            s.user_id,
                            s.bazar_id,
                            b.bazar_name,
                            u.username,
                            s.bazar_cat,
                            s.server_flag,
                            s.cancel_status,
                            s.game_date,
                            s.upload_date,
                            s.created_date,
                            'Single' AS game_type,
                            NULL AS pana,
                            NULL AS jodi,
                            (
                                IFNULL(s.single0,0) + IFNULL(s.single1,0) + IFNULL(s.single2,0) + 
                                IFNULL(s.single3,0) + IFNULL(s.single4,0) + IFNULL(s.single5,0) + 
                                IFNULL(s.single6,0) + IFNULL(s.single7,0) + IFNULL(s.single8,0) + 
                                IFNULL(s.single9,0)
                            ) AS amount,
                            (
                                SELECT GROUP_CONCAT(i || '-' || value)
                                FROM (
                                    SELECT '0' AS i, s.single0 AS value UNION ALL
                                    SELECT '1', s.single1 UNION ALL
                                    SELECT '2', s.single2 UNION ALL
                                    SELECT '3', s.single3 UNION ALL
                                    SELECT '4', s.single4 UNION ALL
                                    SELECT '5', s.single5 UNION ALL
                                    SELECT '6', s.single6 UNION ALL
                                    SELECT '7', s.single7 UNION ALL
                                    SELECT '8', s.single8 UNION ALL
                                    SELECT '9', s.single9
                                ) WHERE value > 0
                            ) AS betArr
                        FROM single_digit s
                        JOIN bazar b ON b.id = s.bazar_id
                        JOIN users u ON u.id = s.user_id

                        UNION ALL

                        SELECT 
                            p.id,
                            p.user_id,
                            p.bazar_id,
                            b.bazar_name,
                            u.username,
                            p.bazar_cat,
                            p.server_flag,
                            p.cancel_status,
                            p.game_date,
                            p.upload_date,
                            p.created_date,
                            'Pana' AS game_type,
                            p.pana AS pana,
                            NULL AS jodi,
                            p.amount AS amount,
                            (p.pana || '-' || p.amount) AS betArr
                        FROM pana p
                        JOIN bazar b ON b.id = p.bazar_id
                        JOIN users u ON u.id = p.user_id

                        UNION ALL

                        SELECT 
                            j.id,
                            j.user_id,
                            j.bazar_id,
                            b.bazar_name,
                            u.username,
                            j.bazar_cat,
                            j.server_flag,
                            j.cancel_status,
                            j.game_date,
                            j.upload_date,
                            j.created_date,
                            'Jodi' AS game_type,
                            NULL AS pana,
                            j.jodi AS jodi,
                            j.amount AS amount,
                            (j.jodi || '-' || j.amount) AS betArr
                        FROM jodi j
                        JOIN bazar b ON b.id = j.bazar_id
                        JOIN users u ON u.id = j.user_id

                        ORDER BY created_date DESC;
                ";

            SQLiteCommand cmd = new SQLiteCommand(query);
            DataTable dt = dbHelper.Read(cmd);

            // Format upload_date
            foreach (DataRow row in dt.Rows)
            {
                row["upload_date"] = row["upload_date"] == DBNull.Value
                    ? "Null"
                    : StaticVar.ConvertToAmPm(row["upload_date"]?.ToString());
            }

            txtDgv.AutoGenerateColumns = false;
            txtDgv.DataSource = dt;
        }


        private Dictionary<int, decimal> ParseSingleDigitInput(string input)
        {
            var dict = new Dictionary<int, decimal>();
            var entries = input.Split(',');

            foreach (var entry in entries)
            {
                var parts = entry.Trim().Split('-');
                if (parts.Length != 2) continue;

                int number = int.Parse(parts[0]);
                decimal amount = decimal.Parse(parts[1]);

                if (dict.ContainsKey(number))
                    dict[number] += amount;
                else
                    dict[number] = amount;
            }

            return dict;
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            frmHistory frmHistory = new frmHistory();
            frmHistory.ShowDialog();
        }
    }
}