using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OfflineOps
{
    public class StaticVar
    {
        private static string serverUrl = "http://localhost:5001/";
        public static string apiServer => serverUrl.EndsWith("/") ? serverUrl : $"{serverUrl}/";

        public static string access_token { get; set; }
        public static string refresh_token { get; set; }
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

        public static DateTime getCurrDateTime()
        {
            return DateTime.UtcNow.AddMinutes(330);
        }

        public static bool IsTokenExpired(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3)
                {
                    return true;
                }
                string output = parts[1].Replace('-', '+').Replace('_', '/');
                switch (output.Length % 4)
                {
                    case 2: output += "=="; break;
                    case 3: output += "="; break;
                }

                var bytes = Convert.FromBase64String(output);
                string payloadJson = Encoding.UTF8.GetString(bytes);

                // Deserialize JSON payload
                dynamic payloadObj = JsonConvert.DeserializeObject<dynamic>(payloadJson);

                // Get exp claim (Unix timestamp)
                long exp = payloadObj.exp;

                // Convert to DateTime
                DateTimeOffset expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                DateTimeOffset now = DateTimeOffset.UtcNow;

                // Compare expiration
                if (now >= expirationTime)
                {
                    return true; // expired
                }

                return false; // still valid
            }
            catch
            {
                return true;
            }
        }

    }
}