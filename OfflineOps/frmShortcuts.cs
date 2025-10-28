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
    public partial class frmShortcuts : Form
    {
        public frmShortcuts()
        {
            InitializeComponent();
            this.Load += frmShortcuts_Load;
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

        private void frmShortcuts_Load(object sender, EventArgs e)
        {
            var shortcuts = new List<ComboItem>
            {
                new ComboItem("All Bazars", "Ctrl + B"),
                new ComboItem("All Players", "Ctrl + P"),
                new ComboItem("All Shortcuts", "Ctrl + S"),
                new ComboItem("Transaction History", "Ctrl + T"),
                new ComboItem("Submit", "Ctrl + Enter"),
                new ComboItem("Exit", "Esc"),
            };

            txtDgv.DataSource = shortcuts;
        }
    }
}
