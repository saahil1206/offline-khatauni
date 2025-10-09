using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace OfflineOps
{
    public class StaticVar
    {
        private static string serverUrl = "http://localhost:5174/";
        public static string apiServer => serverUrl.EndsWith("/") ? serverUrl : $"{serverUrl}/";

        public static string key { get; set; }
        public static string dataText { get; set; } = "ops-data";
        public static string dataExt { get; set; } = "zip";
        public static string dataSource { get; set; } = "Data Source";


        public static bool CheckInternetConnection()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            string[] hosts = { "8.8.8.8", "1.1.1.1", "208.67.222.222" }; // Google, Cloudflare, OpenDNS

            foreach (string host in hosts)
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        PingReply reply = ping.Send(host, 2000);
                        if (reply.Status == IPStatus.Success)
                            return true;
                    }
                }
                catch { }
            }

            return false;
        }
    }
}