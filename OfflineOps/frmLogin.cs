using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace OfflineOps
{
    public partial class frmLogin : Form
    {
        public frmLogin()
        {
            InitializeComponent();
            this.btnLogin.Click += btnLogin_Click;
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

            if (keyData == Keys.Enter) {
                btnLogin_Click(btnLogin,EventArgs.Empty); return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Text = "Logging in...";
            btnLogin.Enabled = false;
            string username = txtUserName.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both username and password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnLogin.Text = "Login";
                btnLogin.Enabled = false;
                return;
            }
            if (!StaticVar.CheckInternetConnection())
            {
                MessageBox.Show("Internet connection required for login.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnLogin.Text = "Login";
                btnLogin.Enabled = false;
                return;
            }
            try
            {
                var data = new { username = username, password = password };
                using (var client = new HttpClient())
                {
                    string jsonData = JsonConvert.SerializeObject(data);
                    StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync($"{StaticVar.apiServer}account/login", content);
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (apiResponse.status)
                        {
                            string access_token = (string)apiResponse?.results?.access_token;
                            string refresh_token = (string)apiResponse?.results?.refresh_token;
                            if (LicenseManager.Activate(username, access_token, refresh_token))
                            {
                                StaticVar.access_token = access_token;
                                StaticVar.refresh_token = refresh_token;
                                MessageBox.Show("Application activated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                this.DialogResult = DialogResult.OK;
                                this.Close();
                            }
                            else
                            {
                                MessageBox.Show("Unable to save license, Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show(apiResponse.message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show(apiResponse.message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                btnLogin.Text = "Login";
                btnLogin.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnLogin.Text = "Login";
                btnLogin.Enabled = true;
            }
        }
    }
}