using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OfflineOps
{
    public class ApiHelper
    {
        public async Task<(bool, string)> RefreshToken()
        {
            bool success = false; string message = string.Empty;
            try
            {
                var data = new { refresh_token = StaticVar.refresh_token };
                using (var client = new HttpClient())
                {
                    string jsonData = JsonConvert.SerializeObject(data);
                    StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync($"{StaticVar.apiServer}account/refresh-token", content);
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (apiResponse.status)
                        {
                            string access_token = (string)apiResponse?.results?.access_token;
                            string refresh_token = (string)apiResponse?.results?.refresh_token;
                            if (LicenseManager.UpdateTokens(access_token, refresh_token))
                            {
                                StaticVar.access_token = access_token;
                                StaticVar.refresh_token = refresh_token;
                                success = true;
                            }
                            else
                            {
                                message = "Unable to update access tokens.";
                            }
                        }
                        else
                        {
                            message = apiResponse.message;
                        }
                    }
                    else
                    {
                        message = apiResponse.message;
                    }
                }
            }
            catch (Exception ex)
            {

                message = ex.Message;
            }
            return (success, message);
        }


        public async Task<(bool, string)> SyncPlayerData(List<long> arrayList)
        {
            bool success = false; string message = string.Empty;
            try
            {
                int pageIndex = 1; bool hasMore = true; int retryCount = 0;
                while (hasMore)
                {
                    if (StaticVar.IsTokenExpired(StaticVar.access_token))
                    {
                        await RefreshToken();
                    }
                    var data = new { page_index = pageIndex, player_ids = (arrayList?.Count > 0 ? arrayList : null) };
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StaticVar.access_token);

                        string jsonData = JsonConvert.SerializeObject(data);
                        StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                        var response = await client.PostAsync($"{StaticVar.apiServer}offline/sync-player", content);
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            if (apiResponse.status)
                            {
                                int total_pages = (int)apiResponse?.results?.pager.total_pages;
                                if (total_pages <= pageIndex) { hasMore = false; success = true; }
                                pageIndex = pageIndex + 1;

                                foreach (var item in apiResponse?.results?.data)
                                {
                                    long id = (long)item.id;
                                    string username = (string)item.username;
                                    string contact = (string)item.contact;
                                    string balance = (string)item.balance;
                                    string aakda_total = (string)item.aakda_total;
                                    string aakda_exposure = (string)item.aakda_exposure;
                                    string pana_total = (string)item.pana_total;
                                    string pana_exposure = (string)item.pana_exposure;
                                    string group_pana_total = (string)item.group_pana_total;
                                    string group_pana_exposure = (string)item.group_pana_exposure;
                                    string jodi_total = (string)item.jodi_total;
                                    string jodi_exposure = (string)item.jodi_exposure;
                                    string credit_amt = (string)item.credit_amt;
                                    string apc_amount = (string)item.apc_amount;
                                    string profit_loss = (string)item.profit_loss;

                                    updateUserRecord(id, username, contact, balance, aakda_total, aakda_exposure,
                                        pana_total, pana_exposure, group_pana_total, group_pana_exposure, jodi_total,
                                        jodi_exposure, credit_amt, apc_amount, profit_loss);
                                }
                            }
                            else
                            {
                                retryCount++;
                                if (retryCount > 5)
                                {
                                    hasMore = false;
                                }
                            }
                        }
                        else
                        {
                            retryCount++;
                            if (retryCount > 5)
                            {
                                hasMore = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                message = ex.Message;
            }
            return (success, message);
        }

        private bool updateUserRecord(long id, string username, string contact, string balance, string aakda_total,
            string aakda_exposure, string pana_total, string pana_exposure, string group_pana_total, string group_pana_exposure,
            string jodi_total, string jodi_exposure, string credit_amt, string apc_amount, string profit_loss)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT 1 FROM users WHERE id = @id"); cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add("@id", DbType.Int64).Value = id;
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            int i = 0;
            if (dt.Rows.Count > 0)
            {
                cmd = new SQLiteCommand(@"UPDATE users SET username = @username, contact = @contact, balance = @balance, aakda_total = @aakda_total, 
                aakda_exposure = @aakda_exposure, pana_total = @pana_total, pana_exposure = @pana_exposure, group_pana_total = @group_pana_total, 
                group_pana_exposure = @group_pana_exposure, jodi_total = @jodi_total, jodi_exposure = @jodi_exposure, credit_amt = @credit_amt, 
                apc_amount = @apc_amount, profit_loss = @profit_loss WHERE id = @id"); cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@username", DbType.String, -1).Value = username;
                cmd.Parameters.Add("@contact", DbType.String, -1).Value = contact;
                cmd.Parameters.Add("@balance", DbType.String, -1).Value = balance;
                cmd.Parameters.Add("@aakda_total", DbType.String, -1).Value = aakda_total;
                cmd.Parameters.Add("@aakda_exposure", DbType.String, -1).Value = aakda_exposure;
                cmd.Parameters.Add("@pana_total", DbType.String, -1).Value = pana_total;
                cmd.Parameters.Add("@pana_exposure", DbType.String, -1).Value = pana_exposure;
                cmd.Parameters.Add("@group_pana_total", DbType.String, -1).Value = group_pana_total;
                cmd.Parameters.Add("@group_pana_exposure", DbType.String, -1).Value = group_pana_exposure;
                cmd.Parameters.Add("@jodi_total", DbType.String, -1).Value = jodi_total;
                cmd.Parameters.Add("@jodi_exposure", DbType.String, -1).Value = jodi_exposure;
                cmd.Parameters.Add("@credit_amt", DbType.String, -1).Value = credit_amt;
                cmd.Parameters.Add("@apc_amount", DbType.String, -1).Value = apc_amount;
                cmd.Parameters.Add("@profit_loss", DbType.String, -1).Value = profit_loss;
                cmd.Parameters.Add("@id", DbType.Int64).Value = id;

                i = databaseHelper.Update(cmd);
            }
            else
            {
                cmd = new SQLiteCommand(@"INSERT INTO users(id, username, contact, balance, aakda_total, aakda_exposure, pana_total, pana_exposure, 
                group_pana_total, group_pana_exposure, jodi_total, jodi_exposure, credit_amt, apc_amount, profit_loss) VALUES(@id, @username, @contact,
                @balance, @aakda_total, @aakda_exposure, @pana_total, @pana_exposure, @group_pana_total, @group_pana_exposure, @jodi_total, @jodi_exposure,
                @credit_amt, @apc_amount, @profit_loss)"); cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@id", DbType.Int64).Value = id;
                cmd.Parameters.Add("@username", DbType.String, -1).Value = username;
                cmd.Parameters.Add("@contact", DbType.String, -1).Value = contact;
                cmd.Parameters.Add("@balance", DbType.String, -1).Value = balance;
                cmd.Parameters.Add("@aakda_total", DbType.String, -1).Value = aakda_total;
                cmd.Parameters.Add("@aakda_exposure", DbType.String, -1).Value = aakda_exposure;
                cmd.Parameters.Add("@pana_total", DbType.String, -1).Value = pana_total;
                cmd.Parameters.Add("@pana_exposure", DbType.String, -1).Value = pana_exposure;
                cmd.Parameters.Add("@group_pana_total", DbType.String, -1).Value = group_pana_total;
                cmd.Parameters.Add("@group_pana_exposure", DbType.String, -1).Value = group_pana_exposure;
                cmd.Parameters.Add("@jodi_total", DbType.String, -1).Value = jodi_total;
                cmd.Parameters.Add("@jodi_exposure", DbType.String, -1).Value = jodi_exposure;
                cmd.Parameters.Add("@credit_amt", DbType.String, -1).Value = credit_amt;
                cmd.Parameters.Add("@apc_amount", DbType.String, -1).Value = apc_amount;
                cmd.Parameters.Add("@profit_loss", DbType.String, -1).Value = profit_loss;

                i = databaseHelper.Update(cmd);
            }
            return i > 0;
        }
    }
}