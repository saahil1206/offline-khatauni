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
using System.Threading;

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

        public async Task<(bool, string)> SyncBazarData()
        {
            bool success = false; string message = string.Empty;
            try
            {
                bool hasMore = true; int retryCount = 0;
                while (hasMore)
                {
                    if (StaticVar.IsTokenExpired(StaticVar.access_token))
                    {
                        await RefreshToken();
                    }

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StaticVar.access_token);

                        string jsonData = JsonConvert.SerializeObject(new { });
                        StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                        var response = await client.PostAsync($"{StaticVar.apiServer}offline/sync-bazar", content);
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            if (apiResponse.status)
                            {
                                hasMore = false; success = true;

                                foreach (var item in apiResponse?.results?.bazarData)
                                {
                                    updateBazarRecord(item);
                                }
                                foreach (var item in apiResponse?.results?.comData)
                                {
                                    updateComRecord(item);
                                }
                                foreach (var item in apiResponse?.results?.liPanaData)
                                {
                                    updateLiPanaRecord(item);
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

                                string sync_date = (string)apiResponse?.results?.sync_date;

                                foreach (var item in apiResponse?.results?.data)
                                {
                                    updateUserRecord(item, sync_date);
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

        private bool syncLoadRunning = false;
        private readonly object syncLock = new object();
        public int syncLoadLimit = 10;

        /// <summary>
        /// Check if sync is currently running
        /// </summary>
        public bool IsSyncRunning
        {
            get
            {
                lock (syncLock)
                {
                    return syncLoadRunning;
                }
            }
        }


        /// <summary>
        /// Perform sync operation - cannot be cancelled once started
        /// </summary>
        public async Task<(bool, string, List<long>)> SyncLoad()
        {
            // Check if already running
            lock (syncLock)
            {
                if (syncLoadRunning)
                {
                    return (false, "Syncing is already in progress.", null);
                }
                syncLoadRunning = true;
            }

            try
            {
                return await SyncLoadInternal();
            }
            finally
            {
                lock (syncLock)
                {
                    syncLoadRunning = false;
                }
            }
        }

        private async Task<(bool success, string message, List<long> idx)> SyncLoadInternal()
        {
            string query = @"
                    SELECT * FROM (
                        SELECT
                            id, bet_id, user_id, bazar_id, bazar_cat, game_name, game_test_name, aakda_no, pana_no,
                            amount, total_amount, server_flag, cancel_status, game_date,
                            upload_date, created_date, 'GROUP' AS type,
                            NULL AS single0, NULL AS single1, NULL AS single2, NULL AS single3, NULL AS single4,
                            NULL AS single5, NULL AS single6, NULL AS single7, NULL AS single8, NULL AS single9
                        FROM group_trans
                        WHERE server_flag = 0 AND cancel_status = 0

                        UNION ALL

                        SELECT
                            id, bet_id, user_id, bazar_id, bazar_cat, NULL AS game_name, NULL AS game_test_name,
                            NULL AS aakda_no, NULL AS pana_no, amount, NULL AS total_amount, server_flag,
                            cancel_status, game_date, upload_date, created_date, 'SINGLE' AS type,
                            single0, single1, single2, single3, single4,
                            single5, single6, single7, single8, single9
                        FROM single_digit
                        WHERE server_flag = 0 AND cancel_status = 0
                    )
                    ORDER BY bet_id, created_date
                    LIMIT @limit;
                ";

            DatabaseHelper db1 = new DatabaseHelper();
            DataTable dt;
            using (var cmd = new SQLiteCommand(query))
            {
                cmd.Parameters.AddWithValue("@limit", syncLoadLimit);
                dt = db1.Read(cmd);
            }

            if (dt.Rows.Count <= 0)
            {
                return (false, "No load data to sync.", null);
            }

            var finalPayloadList = new List<Dictionary<string, object>>();

            var grouped = dt.AsEnumerable()
                .GroupBy(r => r["bet_id"]?.ToString())
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var group in grouped)
            {
                string betId = group.Key;

                string betStr = null; long amount = 0;
                using (var cmdStr = new SQLiteCommand("SELECT bet_str,total_amount FROM bet_request WHERE id = @id"))
                {
                    cmdStr.Parameters.AddWithValue("@id", betId);
                    DataTable strDt = db1.Read(cmdStr);
                    if (strDt.Rows.Count > 0) { betStr = strDt.Rows[0]["bet_str"]?.ToString(); long.TryParse(strDt.Rows[0]["total_amount"].ToString(), out amount); }
                }

                var payload = new Dictionary<string, object>
                {
                    ["bet_id"] = betId,
                    ["bet_str"] = betStr,
                    ["amount"] = amount,
                    ["data"] = group.Select(row =>
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (DataColumn col in dt.Columns)
                            dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                        return dict;
                    }).ToList()
                };

                finalPayloadList.Add(payload);
            }

            if (finalPayloadList.Count == 0)
            {
                return (false, "No valid grouped data found.", null);
            }

            if (StaticVar.IsTokenExpired(StaticVar.access_token))
            {
                await RefreshToken();
            }


            using (var db = new DatabaseHelper())
            {
                //db.BeginTransaction();
                try
                {
                    // UPDATE SELECTED ROWS 
                    //List<string> singleIds = new List<string>(); List<string> groupIds = new List<string>();
                    //foreach (var group in finalPayloadList)
                    //{
                    //    var rows = (List<Dictionary<string, object>>)group["data"];
                    //    foreach (var row in rows)
                    //    {
                    //        string id = row["id"].ToString();
                    //        string type = row["type"].ToString().ToLower();
                    //        if (type == "group") groupIds.Add(id);
                    //        else singleIds.Add(id);
                    //    }
                    //}
                    //string uploadDate = StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss");

                    //if (singleIds.Count > 0)
                    //{
                    //    string placeholders = string.Join(",", singleIds.Select((_, i) => $"@id{i}"));
                    //    string updateQuery = $"UPDATE single_digit SET server_flag = 1, upload_date = @upload_date WHERE id IN ({placeholders})";
                    //    using (var updateCmd = new SQLiteCommand(updateQuery))
                    //    {
                    //        updateCmd.Parameters.AddWithValue("@upload_date", uploadDate);
                    //        for (int i = 0; i < singleIds.Count; i++)
                    //            updateCmd.Parameters.AddWithValue($"@id{i}", singleIds[i]);
                    //        db.Update(updateCmd);
                    //    }
                    //}

                    //if (groupIds.Count > 0)
                    //{
                    //    string placeholders = string.Join(",", groupIds.Select((_, i) => $"@id{i}"));
                    //    string updateQuery = $"UPDATE group_trans SET server_flag = 1, upload_date = @upload_date WHERE id IN ({placeholders})";
                    //    using (var updateCmd = new SQLiteCommand(updateQuery))
                    //    {
                    //        updateCmd.Parameters.AddWithValue("@upload_date", uploadDate);
                    //        for (int i = 0; i < groupIds.Count; i++)
                    //            updateCmd.Parameters.AddWithValue($"@id{i}", groupIds[i]);
                    //        db.Update(updateCmd);
                    //    }
                    //}

                    // Send to server with retry logic
                    int retryCount = 0; int maxRetry = 3;
                    bool uploadSuccess = false; string errorMessage = ""; List<long> userIdx = new List<long>();
                    while (retryCount <= maxRetry)
                    {
                        try
                        {


                            var data = JsonConvert.SerializeObject(finalPayloadList);

                            using (var client = new HttpClient())
                            {
                                client.Timeout = TimeSpan.FromMinutes(2);
                                client.DefaultRequestHeaders.Authorization =
                                    new AuthenticationHeaderValue("Bearer", StaticVar.access_token);

                                var content = new StringContent(data, Encoding.UTF8, "application/json");
                                var response = await client.PostAsync(
                                    $"{StaticVar.apiServer}offline/sync-load",
                                    content);

                                string jsonResponse = await response.Content.ReadAsStringAsync();
                                ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);

                                if (response.StatusCode == HttpStatusCode.OK && apiResponse.status)
                                {
                                    uploadSuccess = true;

                                    if (apiResponse.results?.userIdx?.Count > 0)
                                    {
                                        foreach (var usr in apiResponse.results.userIdx)
                                        {
                                            using (var userCmd = new SQLiteCommand(@"
                                                    UPDATE users 
                                                    SET 
                                                        balance = @balance,
                                                        aakda_total = @aakda_total,
                                                        aakda_exposure = @aakda_exposure,
                                                        pana_total = @pana_total,
                                                        pana_exposure = @pana_exposure,
                                                        group_pana_total = @group_pana_total,
                                                        group_pana_exposure = @group_pana_exposure,
                                                        jodi_total = @jodi_total,
                                                        jodi_exposure = @jodi_exposure,
                                                        credit_amt = @credit_amt,
                                                        opening_credit = @opening_credit,
                                                        apc_amount = @apc_amount,
                                                        opening_credit = @opening_credit,
                                                        profit_loss = @profit_loss,
                                                        sync_date = @sync_date
                                                    WHERE id = @id;
                                                "))
                                            {
                                                userCmd.Parameters.AddWithValue("@id", usr.id);
                                                userCmd.Parameters.AddWithValue("@balance", usr.balance);
                                                userCmd.Parameters.AddWithValue("@aakda_total", usr.aakda_total);
                                                userCmd.Parameters.AddWithValue("@aakda_exposure", usr.aakda_exposure);
                                                userCmd.Parameters.AddWithValue("@pana_total", usr.pana_total);
                                                userCmd.Parameters.AddWithValue("@pana_exposure", usr.pana_exposure);
                                                userCmd.Parameters.AddWithValue("@group_pana_total", usr.group_pana_total);
                                                userCmd.Parameters.AddWithValue("@group_pana_exposure", usr.group_pana_exposure);
                                                userCmd.Parameters.AddWithValue("@jodi_total", usr.jodi_total);
                                                userCmd.Parameters.AddWithValue("@jodi_exposure", usr.jodi_exposure);
                                                userCmd.Parameters.AddWithValue("@credit_amt", usr.credit_amt);
                                                userCmd.Parameters.AddWithValue("@opening_credit", usr.opening_credit);
                                                userCmd.Parameters.AddWithValue("@opening_credit", usr.opening_credit);
                                                userCmd.Parameters.AddWithValue("@apc_amount", usr.apc_amount);
                                                userCmd.Parameters.AddWithValue("@profit_loss", usr.profit_loss);
                                                userCmd.Parameters.AddWithValue("@sync_date", StaticVar.getCurrDateTime().ToString("yyyy-MM-dd"));

                                                db.Update(userCmd);
                                            }
                                        }
                                    }

                                    string uploadDate = StaticVar.getCurrDateTime().ToString("yyyy-MM-dd HH:mm:ss");

                                    if (apiResponse.results?.betIdx?.Count > 0)
                                    {
                                        var betIdx = ((IEnumerable<object>)apiResponse.results.betIdx)
                                                     .Select(x => x.ToString())
                                                     .ToList();
                                        if (betIdx.Count > 0)
                                        {
                                            var placeholders = betIdx.Select((id, i) => $"@bet{i}").ToArray();
                                            string inClause = string.Join(",", placeholders);
                                            string betQuery = $@"
                                                    UPDATE single_digit SET server_flag = 1, upload_date = @upload_date WHERE bet_id IN ({inClause});
                                                    UPDATE group_trans SET server_flag = 1, upload_date = @upload_date WHERE bet_id IN ({inClause});
                                                    UPDATE bet_request SET server_flag = 1, upload_date = @upload_date WHERE id IN ({inClause});
                                                ";
                                            using (var betCmd = new SQLiteCommand(betQuery))
                                            {
                                                betCmd.Parameters.AddWithValue("@upload_date", uploadDate);
                                                for (int i = 0; i < betIdx.Count; i++)
                                                {
                                                    betCmd.Parameters.AddWithValue($"@bet{i}", betIdx[i]);
                                                }

                                                db.Update(betCmd);
                                            }
                                        }
                                    }


                                    if (apiResponse.results.userIdx is IEnumerable<dynamic> usersList)
                                    {
                                        foreach (var user in usersList)
                                        {
                                            long idValue = 0;

                                            if (user.id != null && long.TryParse(user.id.ToString(), out idValue))
                                            {
                                                userIdx.Add(idValue);
                                            }
                                        }
                                    }

                                    break; // Success, exit retry loop
                                }
                                else
                                {
                                    errorMessage = apiResponse?.message ?? "Server returned error";
                                    retryCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errorMessage = ex.Message;
                            retryCount++;

                            // Exponential backoff: wait before retry
                            if (retryCount <= maxRetry)
                            {
                                await Task.Delay(500 * retryCount); // 1s, 2s, 3s
                            }
                        }
                    }
                    if (uploadSuccess)
                    {
                        return (true, "Synced successfully.", userIdx);
                    }
                    else
                    {
                        return (false, $"Failed to sync after {maxRetry} retries. Error: {errorMessage}", null);
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Sync error: {ex.Message}", null);
                }
            }
        }


        private bool updateBazarRecord(dynamic item)
        {
            try
            {
                using (var connection = new SQLiteConnection(DatabaseManager.ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Single upsert query for bazar table with day columns
                            string upsertBazarQuery = @"
                          INSERT INTO bazar (
                              id, bazar_unique, bazar_name, open_time, close_time,
                              open_start_time, close_start_time, total_days, open_block_time,
                              close_block_time, status, is_madhur_exp, start_status,
                              admin_opentime, admin_closetime, wa_start_status, bazar_section_id,
                              new_open_time, new_close_time, bazar_code,
                              monday, tuesday, wednesday, Thursday, friday, saturday, sunday
                          ) VALUES (
                              @id, @bazar_unique, @bazar_name, @open_time, @close_time,
                              @open_start_time, @close_start_time, @total_days, @open_block_time,
                              @close_block_time, @status, @is_madhur_exp, @start_status,
                              @admin_opentime, @admin_closetime, @wa_start_status, @bazar_section_id,
                              @new_open_time, @new_close_time, @bazar_code,
                              @monday, @tuesday, @wednesday, @Thursday, @friday, @saturday, @sunday
                          )
                          ON CONFLICT(id) DO UPDATE SET
                              bazar_unique = excluded.bazar_unique,
                              bazar_name = excluded.bazar_name,
                              open_time = excluded.open_time,
                              close_time = excluded.close_time,
                              open_start_time = excluded.open_start_time,
                              close_start_time = excluded.close_start_time,
                              total_days = excluded.total_days,
                              open_block_time = excluded.open_block_time,
                              close_block_time = excluded.close_block_time,
                              status = excluded.status,
                              is_madhur_exp = excluded.is_madhur_exp,
                              start_status = excluded.start_status,
                              admin_opentime = excluded.admin_opentime,
                              admin_closetime = excluded.admin_closetime,
                              wa_start_status = excluded.wa_start_status,
                              bazar_section_id = excluded.bazar_section_id,
                              new_open_time = excluded.new_open_time,
                              new_close_time = excluded.new_close_time,
                              bazar_code = excluded.bazar_code,
                              monday = excluded.monday,
                              tuesday = excluded.tuesday,
                              wednesday = excluded.wednesday,
                              Thursday = excluded.Thursday,
                              friday = excluded.friday,
                              saturday = excluded.saturday,
                              sunday = excluded.sunday";

                            using (var cmd = new SQLiteCommand(upsertBazarQuery, connection, transaction))
                            {
                                // Main bazar fields
                                cmd.Parameters.AddWithValue("@id", (long)item.id);
                                cmd.Parameters.AddWithValue("@bazar_unique", item.bazar_unique?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@bazar_name", item.bazar_name?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@open_time", item.open_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@close_time", item.close_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@open_start_time", item.open_start_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@close_start_time", item.close_start_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@total_days", item.total_days?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@open_block_time", item.open_block_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@close_block_time", item.close_block_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@status", item.status?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@is_madhur_exp", item.is_madhur_exp?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@start_status", item.start_status?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@admin_opentime", item.admin_opentime?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@admin_closetime", item.admin_closetime?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@wa_start_status", item.wa_start_status?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@bazar_section_id", item.bazar_section_id?.ToString() ?? "1");
                                cmd.Parameters.AddWithValue("@new_open_time", item.new_open_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@new_close_time", item.new_close_time?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@bazar_code", item.bazar_code?.ToString() ?? "");

                                // Day fields from nested days object
                                cmd.Parameters.AddWithValue("@monday", item.days?.monday?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@tuesday", item.days?.tuesday?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@wednesday", item.days?.wednesday?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@Thursday", item.days?.Thursday?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@friday", item.days?.friday?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@saturday", item.days?.saturday?.ToString() ?? "0");
                                cmd.Parameters.AddWithValue("@sunday", item.days?.sunday?.ToString() ?? "0");

                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"Error updating bazar record: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database error in updateBazarRecord: {ex.Message}");
            }
            return false;
        }

        private bool updateComRecord(dynamic item)
        {
            try
            {
                using (var connection = new SQLiteConnection(DatabaseManager.ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Single upsert query for bazar table with day columns
                            string upsertBazarQuery = @"
                          INSERT INTO li_com (
                              com_id, com_display_name, com_name, com_function,game_name, com_min_no,
                              com_max_no, com_status
                          ) VALUES (
                              @com_id, @com_display_name, @com_name,@com_function ,@game_name, @com_min_no,
                              @com_max_no, @com_status
                          )
                          ON CONFLICT(com_id) DO UPDATE SET
                              com_display_name = excluded.com_display_name,
                              com_name = excluded.com_name,
                              com_function = excluded.com_function,
                              game_name = excluded.game_name,
                              com_min_no = excluded.com_min_no,
                              com_max_no = excluded.com_max_no,
                              com_status = excluded.com_status";

                            using (var cmd = new SQLiteCommand(upsertBazarQuery, connection, transaction))
                            {
                                // Main bazar fields
                                cmd.Parameters.AddWithValue("@com_id", (long)item.com_id);
                                cmd.Parameters.AddWithValue("@com_display_name", item.com_display_name?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@com_name", item.com_name?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@com_function", item.com_function?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@game_name", item.game_name?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@com_min_no", item.com_min_no?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@com_max_no", item.com_max_no?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@com_status", item.com_status?.ToString() ?? "");

                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"Error updating com record: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database error in updateComRecord: {ex.Message}");
            }
            return false;
        }

        private bool updateLiPanaRecord(dynamic item)
        {
            try
            {
                using (var connection = new SQLiteConnection(DatabaseManager.ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Single upsert query for bazar table with day columns
                            string upsertBazarQuery = @"
                              INSERT INTO li_pana (
                                  id, pana, group_id, number_id,number_main,status, pana_type,
                                  check_motor, check_family,check_chipke_bikhre, check_run, check_center, check_forgot
                              ) VALUES (
                                  @id, @pana, @group_id,@number_id ,@number_main,@status ,@pana_type,
                                  @check_motor, @check_family, @check_chipke_bikhre, @check_run, @check_center, @check_forgot
                              )
                              ON CONFLICT(id) DO UPDATE SET
                                  pana = excluded.pana,
                                  group_id = excluded.group_id,
                                  number_id = excluded.number_id,
                                  number_main = excluded.number_main,
                                  status = excluded.status,
                                  pana_type = excluded.pana_type,
                                  check_motor = excluded.check_motor,
                                  check_family = excluded.check_family,
                                  check_chipke_bikhre = excluded.check_chipke_bikhre,
                                  check_run = excluded.check_run,
                                  check_center = excluded.check_center,
                                  check_forgot = excluded.check_forgot
                             ";

                            using (var cmd = new SQLiteCommand(upsertBazarQuery, connection, transaction))
                            {
                                // Main bazar fields
                                cmd.Parameters.AddWithValue("@id", (long)item.id);
                                cmd.Parameters.AddWithValue("@pana", item.pana?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@group_id", item.group_id?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@number_id", item.number_id?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@number_main", item.number_main?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@status", item.status?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@pana_type", item.pana_type?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@check_motor", item.check_motor?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@check_family", item.check_family?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@check_chipke_bikhre", item.check_chipke_bikhre?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@check_run", item.check_run?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@check_center", item.check_center?.ToString() ?? "");
                                cmd.Parameters.AddWithValue("@check_forgot", item.check_forgot?.ToString() ?? "");

                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"Error updating com record: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database error in updateComRecord: {ex.Message}");
            }
            return false;
        }

        private bool updateUserRecord(PlayerData itm, string sync_date)
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
                                            jodi_total, jodi_exposure, credit_amt,opening_credit, apc_amount, profit_loss, sync_date)
                          VALUES (@id, @username, @contact, @balance, @aakda_total, @aakda_exposure,
                                 @pana_total, @pana_exposure, @group_pana_total, @group_pana_exposure,
                                 @jodi_total, @jodi_exposure, @credit_amt,@opening_credit ,@apc_amount, @profit_loss, @sync_date)
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
                              opening_credit = excluded.opening_credit,
                              apc_amount = excluded.apc_amount,                              
                              profit_loss = excluded.profit_loss,
                              sync_date = excluded.sync_date";
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
                            cmd.Parameters.AddWithValue("@opening_credit", itm.opening_credit);
                            cmd.Parameters.AddWithValue("@apc_amount", itm.apc_amount);
                            cmd.Parameters.AddWithValue("@profit_loss", itm.profit_loss);
                            cmd.Parameters.AddWithValue("@sync_date", sync_date);
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