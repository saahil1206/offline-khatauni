using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfflineOps
{
    public class StaticVar
    {
        public static string key { get; set; }
        public static string passText { get; set; } = "Password";
        public static string dataText { get; set; } = "Data";
        public static string sourceText { get; set; } = "Source";
        public static string dbNameText { get; set; } = "StarwwinData";
    }
}