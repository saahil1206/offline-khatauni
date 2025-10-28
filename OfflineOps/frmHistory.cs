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

namespace OfflineOps
{
    public partial class frmHistory : Form
    {
        public frmHistory()
        {
            InitializeComponent();
            this.Load += frmHistory_Load;
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

        private void frmHistory_Load(object sender, EventArgs e)
        {
            SQLiteCommand cmd = new SQLiteCommand(@"SELECT  s.*, b.bazar_name,u.username FROM single_digit s JOIN bazar b ON b.id = s.bazar_id JOIN users u ON u.id = s.user_id ORDER BY s.created_date DESC;"); cmd.CommandType = CommandType.Text;
            DatabaseHelper dbHelper = new DatabaseHelper(); DataTable dt = dbHelper.Read(cmd);
            for (int i = 0; i < dt.Columns.Count; i++) { dt.Columns[i].ReadOnly = false; }
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                dt.Rows[i]["server_flag"] = Convert.ToBoolean(dt.Rows[i]["server_flag"]);
                dt.Rows[i]["upload_date"] = dt.Rows[i]["upload_date"] == DBNull.Value ? "Null" : StaticVar.ConvertToAmPm(dt.Rows[i]["upload_date"]?.ToString());
            }
            txtDgv.AutoGenerateColumns = false;
            txtDgv.DataSource = dt;
        }
    }
}
