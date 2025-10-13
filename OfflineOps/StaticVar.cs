using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

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

        public static string ConvertToAmPm(string time24)
        {
            if (DateTime.TryParseExact(time24, "HH:mm:ss",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out DateTime time))
            {
                return time.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }
            else
            {
                throw new FormatException("Invalid time format. Expected format: HH:mm:ss");
            }
        }

        public static DateTime getGameCurrDate()
        {
            DateTime now = getCurrDateTime();
            DateTime gameDayStart = new DateTime(now.Year, now.Month, now.Day, 1, 0, 0); // 1:00 AM today

            DateTime resultDate;

            if (now < gameDayStart)
            {
                resultDate = now.AddDays(-1); // use yesterday
            }
            else
            {
                resultDate = now;
            }
            return resultDate;
        }

        public static string getGameCurrDateStr()
        {
            return getGameCurrDate().ToString("yyyy-MM-dd");
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