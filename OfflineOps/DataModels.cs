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

    public class PlayerResponse
    {
        public bool status { get; set; }
        public PlayerResults results { get; set; }
    }

    public class PlayerResults
    {
        public Pager pager { get; set; }
        public List<PlayerData> data { get; set; }
        public string sync_date { get; set; }
    }

    public class Pager
    {
        public int total_record { get; set; }
        public int total_pages { get; set; }
        public int current_page { get; set; }
        public int page_size { get; set; }
    }

    public class PlayerData
    {
        public long id { get; set; }
        public string username { get; set; }
        public string contact { get; set; }
        public decimal balance { get; set; }
        public decimal aakda_total { get; set; }
        public decimal aakda_exposure { get; set; }
        public decimal pana_total { get; set; }
        public decimal pana_exposure { get; set; }
        public decimal group_pana_total { get; set; }
        public decimal group_pana_exposure { get; set; }
        public decimal jodi_total { get; set; }
        public decimal jodi_exposure { get; set; }
        public decimal credit_amt { get; set; }
        public decimal apc_amount { get; set; }
        public decimal profit_loss { get; set; }
        public List<long> games { get; set; }  // Now properly typed!
    }
}