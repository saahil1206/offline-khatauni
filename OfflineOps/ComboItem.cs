using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfflineOps
{
    public class ComboItem
    {
        public string Text { get; set; }
        public object Value { get; set; }

        public ComboItem(string text, object value)
        {
            Text = text;
            Value = value;
        }

        // This defines what shows in the ComboBox dropdown
        public override string ToString()
        {
            return Text;
        }
    }
}
