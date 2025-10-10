using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfflineOps
{
    public class ApiResponse
    {
        public ApiResponse()
        {
            status = false;
        }
        public bool status { get; set; }
        public string code { get; set; }
        public string message { get; set; }
        public dynamic results { get; set; }
    }
}