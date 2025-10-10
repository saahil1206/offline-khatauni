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
using System.Linq;

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
                        PlayerResponse apiResponse = JsonConvert.DeserializeObject<PlayerResponse>(jsonResponse);
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            if (apiResponse.status)
                            {
                                int total_pages = (int)apiResponse?.results?.pager.total_pages;
                                if (total_pages <= pageIndex) { hasMore = false; success = true; }
                                pageIndex = pageIndex + 1;

                                foreach (var item in apiResponse?.results?.data)
                                {

                                    updateUserRecord(item);
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


        private bool updateUserRecord(PlayerData itm)
        {
            using (var connection = new SQLiteConnection(DatabaseManager.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Update or Insert main user record
                        string upsertUserQuery = @"
                          INSERT INTO users (id, username, contact, balance, aakda_total, aakda_exposure,
                                            pana_total, pana_exposure, group_pana_total, group_pana_exposure,
                                            jodi_total, jodi_exposure, credit_amt, apc_amount, profit_loss)
                          VALUES (@id, @username, @contact, @balance, @aakda_total, @aakda_exposure,
                                 @pana_total, @pana_exposure, @group_pana_total, @group_pana_exposure,
                                 @jodi_total, @jodi_exposure, @credit_amt, @apc_amount, @profit_loss)
                          ON CONFLICT(id) DO UPDATE SET
                              username = excluded.username,
                              contact = excluded.contact,
                              balance = excluded.balance,
                              aakda_total = excluded.aakda_total,
                              aakda_exposure = excluded.aakda_exposure,
                              pana_total = excluded.pana_total,
                              pana_exposure = excluded.pana_exposure,
                              group_pana_total = excluded.group_pana_total,
                              group_pana_exposure = excluded.group_pana_exposure,
                              jodi_total = excluded.jodi_total,
                              jodi_exposure = excluded.jodi_exposure,
                              credit_amt = excluded.credit_amt,
                              apc_amount = excluded.apc_amount,
                              profit_loss = excluded.profit_loss";
                        using (var cmd = new SQLiteCommand(upsertUserQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", itm.id);
                            cmd.Parameters.AddWithValue("@username", itm.username);
                            cmd.Parameters.AddWithValue("@contact", itm.contact);
                            cmd.Parameters.AddWithValue("@balance", itm.balance);
                            cmd.Parameters.AddWithValue("@aakda_total", itm.aakda_total);
                            cmd.Parameters.AddWithValue("@aakda_exposure", itm.aakda_exposure);
                            cmd.Parameters.AddWithValue("@pana_total", itm.pana_total);
                            cmd.Parameters.AddWithValue("@pana_exposure", itm.pana_exposure);
                            cmd.Parameters.AddWithValue("@group_pana_total", itm.group_pana_total);
                            cmd.Parameters.AddWithValue("@group_pana_exposure", itm.group_pana_exposure);
                            cmd.Parameters.AddWithValue("@jodi_total", itm.jodi_total);
                            cmd.Parameters.AddWithValue("@jodi_exposure", itm.jodi_exposure);
                            cmd.Parameters.AddWithValue("@credit_amt", itm.credit_amt);
                            cmd.Parameters.AddWithValue("@apc_amount", itm.apc_amount);
                            cmd.Parameters.AddWithValue("@profit_loss", itm.profit_loss);
                            cmd.ExecuteNonQuery();
                        }
                        // 2. Get existing games for this user from database
                        var existingGames = new List<long>();
                        string selectGamesQuery = "SELECT game_id FROM user_games WHERE user_id = @user_id";
                        using (var cmd = new SQLiteCommand(selectGamesQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@user_id", itm.id);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    existingGames.Add(reader.GetInt64(0));
                                }
                            }
                        }
                        // 3. Determine which games to delete (in DB but not in new list)
                        var gamesToDelete = existingGames.Except(itm.games ?? new List<long>()).ToList();

                        // 4. Determine which games to insert (in new list but not in DB)
                        var gamesToInsert = (itm.games ?? new List<long>()).Except(existingGames).ToList();

                        // 5. Delete games that are no longer in the list
                        if (gamesToDelete.Any())
                        {
                            string placeholders = string.Join(",", gamesToDelete.Select((_, i) => $"@game_{i}"));
                            string deleteQuery = $"DELETE FROM user_games WHERE user_id = @user_id AND game_id IN ({placeholders})";

                            using (var cmd = new SQLiteCommand(deleteQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@user_id", itm.id);
                                for (int i = 0; i < gamesToDelete.Count; i++)
                                {
                                    cmd.Parameters.AddWithValue($"@game_{i}", gamesToDelete[i]);
                                }
                                cmd.ExecuteNonQuery();
                            }
                        }
                        // 6. Insert new games
                        if (gamesToInsert.Any())
                        {
                            string insertQuery = "INSERT INTO user_games (user_id, game_id) VALUES (@user_id, @game_id)";
                            foreach (var gameId in gamesToInsert)
                            {
                                using (var cmd = new SQLiteCommand(insertQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@user_id", itm.id);
                                    cmd.Parameters.AddWithValue("@game_id", gameId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Error updating user record: {ex.Message}");
                    }
                }
            }
            return false;
        }
    }
}