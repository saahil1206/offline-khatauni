using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OfflineOps
{
    #region Game Processor

    public class GameProcessor
    {
        private bool is_group = false;
        public string group_game_name = "";
        public string group_test_name = "";
        private DatabaseHelper dbHelper;
        private GroupGameModel groupGameModel;

        public GameProcessor()
        {
            dbHelper = new DatabaseHelper();
            groupGameModel = new GroupGameModel(dbHelper);
        }

        public Dictionary<string, object> ProcessGameInput(string strin, long currenttime, long blocktime, long nblocktime)
        {
            var status_res = new List<object>();
            //int is_in_admin_time = 0;

            //if (currenttime < blocktime)
            //{
            //    if (currenttime < nblocktime)
            //    {
            //        is_in_admin_time = 0;
            //    }
            //    else
            //    {
            //        is_in_admin_time = 1;
            //    }

            string strinNew = CheckStringForCommand(strin);
            if (!string.IsNullOrEmpty(strinNew))
            {
                strin = strinNew;
            }

            string[] skuList = strin.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            double total_amt = 0;
            string msgstring = "";
            int sl = 0;

            var single_aakda = new Dictionary<string, double>();
            var single_pana = new List<string>();
            var single_jodi = new List<string>();
            var group_pana = new List<string>();

            double grp_tlt_amt = 0;
            double aakda_tlt_amt = 0;
            double pana_tlt_amt = 0;
            double jodi_tlt_amt = 0;
            int total_numbers = 0;
            string numbers_new = "";

            foreach (string data in skuList)
            {
                sl++;
                string[] amt_nu = MultiExplode(new[] { ':', '=', '*', '-', '+' }, data);
                int i = 0;
                int len = amt_nu.Length;
                int islastelement = 0;
                var numarray = new List<string>();

                foreach (string item in amt_nu)
                {
                    if (i == len - 1)
                    {
                        islastelement = 1;
                    }

                    if (islastelement == 0)
                    {
                        string[] amt = MultiExplode(
                            new[] { ':', ';', '@', ' ', '&', '~', ',', '.', '/' },
                            amt_nu[i + 1]
                        );

                        if (i == 0)
                        {
                            numarray.Add(item + "=" + amt[0]);
                        }
                        else
                        {
                            string[] firstval = MultiExplode(
                                new[] { ':', ';', '@', ' ', '&', '~', ',', '.', '/' },
                                amt_nu[i]
                            );

                            var firstvalList = firstval.ToList();
                            if (firstvalList.Count > 0)
                            {
                                firstvalList.RemoveAt(0);
                            }
                            string stringnew = string.Join(" ", firstvalList);
                            numarray.Add(stringnew + "=" + amt[0]);
                        }
                    }
                    i++;
                }

                foreach (string numarraval in numarray)
                {
                    amt_nu = MultiExplode(new[] { ':', '=', '*', '-', '+' }, numarraval);

                    if (!string.IsNullOrEmpty(amt_nu[1]) && !string.IsNullOrEmpty(amt_nu[0]))
                    {
                        string[] numbers = MultiExplode(
                            new[] { ',', '.', '/', ':', ':', ' ' },
                            amt_nu[0]
                        );

                        foreach (string numarry in numbers)
                        {
                            string notpanna = "";

                            // Database query to get pana list
                            var panaList = GetPanaFromDatabase();
                            string[] pannastring = panaList.ToArray();

                            if (numarry.Length >= 3)
                            {
                                if (!pannastring.Contains(numarry))
                                {
                                    notpanna = "1";
                                }
                            }
                            else
                            {
                                notpanna = "";
                            }

                            if (notpanna != "1")
                            {
                                if (!string.IsNullOrEmpty(numarry))
                                {
                                    double amountValue = 0;
                                    double.TryParse(amt_nu[1], out amountValue);

                                    if (numarry.Length == 1)
                                    {
                                        //string number;
                                        //if (numarry == "0")
                                        //{
                                        //    number = "single9";
                                        //}
                                        //else
                                        //{
                                        //    int numValue = int.Parse(numarry);
                                        //    number = "single" + (numValue - 1);
                                        //}
                                        int numValue = int.Parse(numarry);
                                        string number = "single" + numValue;

                                        if (single_aakda.ContainsKey(number))
                                        {
                                            single_aakda[number] += amountValue;
                                        }
                                        else
                                        {
                                            single_aakda[number] = amountValue;
                                        }

                                        aakda_tlt_amt += amountValue;
                                        numbers_new = numbers_new + " " + numarry + "=" + amt_nu[1];
                                    }
                                    else if (numarry.Length == 2)
                                    {
                                        string number = numarry + "=" + amt_nu[1];
                                        single_jodi.Add(number);
                                        jodi_tlt_amt += amountValue;
                                        numbers_new = numbers_new + " " + number;
                                    }
                                    else if (numarry.Length == 3)
                                    {
                                        if (this.is_group == false)
                                        {
                                            string number = numarry + "=" + amt_nu[1];
                                            single_pana.Add(number);
                                            pana_tlt_amt += amountValue;
                                            numbers_new = numbers_new + " " + number;
                                        }
                                        else
                                        {
                                            group_pana.Add(numarry);
                                            grp_tlt_amt += amountValue;
                                            total_numbers++;
                                            numbers_new = numbers_new + " " + numarry + "=" + amt_nu[1];
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Return results
            return new Dictionary<string, object>
                {
                    { "single_aakda", single_aakda },
                    { "single_pana", single_pana },
                    { "single_jodi", single_jodi },
                    { "group_pana", group_pana },
                    { "grp_tlt_amt", grp_tlt_amt },
                    { "aakda_tlt_amt", aakda_tlt_amt },
                    { "pana_tlt_amt", pana_tlt_amt },
                    { "jodi_tlt_amt", jodi_tlt_amt },
                    { "total_numbers", total_numbers },
                    { "numbers_new", numbers_new }
                };
            //}

            //return new Dictionary<string, object>();
        }

        private List<string> GetPanaFromDatabase()
        {
            var panaList = new List<string>();
            try
            {
                var cmd = new SQLiteCommand("SELECT pana FROM li_pana WHERE status != 0");
                DataTable dt = dbHelper.Read(cmd);

                foreach (DataRow row in dt.Rows)
                {
                    panaList.Add(row["pana"].ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching pana: {ex.Message}");
            }
            return panaList;
        }

        public string CheckStringForCommand(string inputString)
        {
            string string_full = inputString.ToUpper();
            string pattern = @"[0-9]+[a-zA-Z]+|[a-zA-Z]+[0-9]+|[a-zA-Z]";
            string result = Regex.Replace(string_full, pattern, "");
            string cleanString = result.Trim();

            string[] arraystr_lastChar22 = MultiExplode(new[] { '=', '*', '-', '+' }, cleanString);
            int wordCount = 0;

            if (arraystr_lastChar22[0].Contains("#"))
            {
                string[] parts = arraystr_lastChar22[0].Split('#');
                string part1 = parts[0];
                string part2 = parts.Length > 1 ? parts[1] : "";
                wordCount = part1.Length;
            }
            else if (string_full.Contains("SPDPTC"))
            {
                string[] parts = arraystr_lastChar22[0].Split(new[] { "  " }, StringSplitOptions.None);
                string part1 = parts[0];
                string part2 = parts.Length > 1 ? parts[1] : "";
                wordCount = part1.Length;
            }
            else if (string_full.Contains("OTC"))
            {
                string[] numbers = arraystr_lastChar22[0].Split(' ');
                wordCount = numbers.Length;
            }
            else
            {
                arraystr_lastChar22[0] = Regex.Replace(arraystr_lastChar22[0], @"\D", "");
                wordCount = arraystr_lastChar22[0].Length;
            }

            // Database query to get command data
            var commandList = GetCommandsFromDatabase();

            foreach (var li_com_data in commandList)
            {
                string cmd_com = li_com_data["com_name"];
                string comand = "";
                string[] cmd = cmd_com.Split(',');

                foreach (string c in cmd)
                {
                    if (string_full.Contains(c))
                    {
                        comand = c;
                    }
                }

                string function_name = li_com_data["com_function"];
                wordCount = arraystr_lastChar22[0].Length;

                if (!string.IsNullOrEmpty(comand))
                {
                    if (string_full.Contains(comand))
                    {
                        string groupName = li_com_data["game_name"];
                        var games = GetGameNames(groupName);
                        this.group_game_name = games.OrgName;
                        this.group_test_name = games.TestName;
                        this.is_group = true;
                        int min_dig = int.Parse(li_com_data["com_min_no"]);
                        int max_dig = int.Parse(li_com_data["com_max_no"]);

                        if (min_dig != max_dig)
                        {
                            string functionResult = CallGroupGameFunction(function_name, arraystr_lastChar22[0]);

                            if (string.IsNullOrEmpty(functionResult))
                            {
                                functionResult = "abc";
                            }

                            if (wordCount <= max_dig && wordCount >= min_dig && functionResult != "abc")
                            {
                                cleanString = functionResult + "=" + arraystr_lastChar22[1];
                            }
                            else
                            {
                                cleanString = "abc";
                            }
                        }
                        else
                        {
                            cleanString = "";
                            var chunks = BreakString(arraystr_lastChar22[0], min_dig);

                            foreach (string chunk in chunks)
                            {
                                string chunkResult = CallGroupGameFunction(function_name, chunk);
                                cleanString = cleanString + "," + chunkResult;

                                if (string.IsNullOrEmpty(chunkResult))
                                {
                                    cleanString = "abc";
                                    break;
                                }
                            }

                            if (cleanString != "abc")
                            {
                                cleanString = cleanString.TrimStart(',');
                                cleanString = cleanString + "=" + arraystr_lastChar22[1];
                            }
                        }
                    }
                }
            }

            return cleanString;
        }

        private List<string> BreakString(string input, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < input.Length; i += chunkSize)
            {
                if (i + chunkSize <= input.Length)
                {
                    chunks.Add(input.Substring(i, chunkSize));
                }
                else
                {
                    chunks.Add(input.Substring(i));
                }
            }
            return chunks;
        }

        private List<Dictionary<string, string>> GetCommandsFromDatabase()
        {
            var commandList = new List<Dictionary<string, string>>();
            try
            {
                var cmd = new SQLiteCommand("SELECT * FROM li_com WHERE com_status = 1");
                DataTable dt = dbHelper.Read(cmd);

                foreach (DataRow row in dt.Rows)
                {
                    var dict = new Dictionary<string, string>();
                    foreach (DataColumn col in dt.Columns)
                    {
                        dict[col.ColumnName] = row[col].ToString();
                    }
                    commandList.Add(dict);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching commands: {ex.Message}");
            }
            return commandList;
        }

        private string CallGroupGameFunction(string functionName, string parameter)
        {
            try
            {
                switch (functionName.ToLower())
                {
                    case "getmotorsp": return groupGameModel.GetMotorSp(parameter);
                    case "getmotordp": return groupGameModel.GetMotorDp(parameter);
                    case "getmotorcutsp": return groupGameModel.GetMotorCutSp(parameter);
                    case "getmotorcutdp": return groupGameModel.GetMotorCutDp(parameter);
                    case "getcommonsp": return groupGameModel.GetCommonSP(parameter);
                    case "getcommondp": return groupGameModel.GetCommonDP(parameter);
                    case "getcommonspdp": return groupGameModel.GetCommonSPDP(parameter);
                    case "getfamily": return groupGameModel.GetFamily(parameter);
                    case "getchipke": return groupGameModel.GetChipke(parameter);
                    case "getbikhre": return groupGameModel.GetBikhre(parameter);
                    case "getrun": return groupGameModel.GetRun(parameter);
                    case "getcenter": return groupGameModel.GetCenter(parameter);
                    case "getforgot": return groupGameModel.GetForgot(parameter);
                    case "getcycle": return groupGameModel.GetCycle(parameter);
                    case "getsp": return groupGameModel.GetSp(parameter);
                    case "getdp": return groupGameModel.GetDp(parameter);
                    case "getspdp": return groupGameModel.GetSpDp(parameter);
                    case "getsptp": return groupGameModel.GetSpTp(parameter);
                    case "getdptp": return groupGameModel.GetDpTp(parameter);
                    case "getspdptp": return groupGameModel.GetSpDpTp(parameter);
                    case "getspstart": return groupGameModel.GetSpStart(parameter);
                    case "getdpstart": return groupGameModel.GetDpStart(parameter);
                    case "getspend": return groupGameModel.GetSpEnd(parameter);
                    case "getdpend": return groupGameModel.GetDpEnd(parameter);
                    case "getspdpstart": return groupGameModel.GetSpDpStart(parameter);
                    case "getspdpend": return groupGameModel.GetSpDpEnd(parameter);
                    case "getmotorspdp": return groupGameModel.GetMotorSpDp(parameter);
                    case "getmotorcutspdp": return groupGameModel.GetMotorCutSpDp(parameter);
                    case "getsabhifigurcut": return groupGameModel.GetSabhiFigurCut(parameter);
                    case "get52pana": return groupGameModel.Get52Pana(parameter);
                    case "get56pana": return groupGameModel.Get56Pana(parameter);
                    case "get83pana": return groupGameModel.Get83Pana(parameter);
                    case "get64pana": return groupGameModel.Get64Pana(parameter);
                    case "get68pana": return groupGameModel.Get68Pana(parameter);
                    case "get72pana": return groupGameModel.Get72Pana(parameter);
                    case "get50pana": return groupGameModel.Get50Pana(parameter);
                    case "getabr30pana": return groupGameModel.GetAbr30Pana(parameter);
                    default:
                        Console.WriteLine($"Unknown function: {functionName}");
                        return "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling function {functionName}: {ex.Message}");
                return "";
            }
        }

        public string[] MultiExplode(char[] delimiters, string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new string[] { "" };
            }

            string ready = input;
            for (int i = 1; i < delimiters.Length; i++)
            {
                ready = ready.Replace(delimiters[i], delimiters[0]);
            }

            string[] launch = ready.Split(delimiters[0]);
            return launch;
        }

        public (string OrgName, string TestName) GetGameNames(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return ("", "");

            string key = groupName.Trim().ToLowerInvariant();
            string orgName = "";
            string testName = groupName;

            switch (key)
            {
                case "sp motor":
                    orgName = "spmotor";
                    testName = "sp motor";
                    break;
                case "sp":
                    orgName = "spPatti";
                    testName = "sp";
                    break;
                case "dp":
                    orgName = "dpPatti";
                    testName = "dp";
                    break;
                case "tp":
                    orgName = "tpPatti";
                    testName = "tp";
                    break;
                case "cycle":
                    orgName = "cyclepana";
                    testName = "cycle";
                    break;
                case "dp + tp":
                    orgName = "dp_tp";
                    testName = "dp + tp";
                    break;
                case "run":
                    orgName = "runpana";
                    testName = "run";
                    break;
                case "dp motor":
                    orgName = "dpmotor";
                    testName = "dp motor";
                    break;
                case "sp motor + dp motor":
                    orgName = "spdpmotor";
                    testName = "sp motor + dp motor";
                    break;
                case "sp motor cut":
                    orgName = "spmotorcut";
                    testName = "sp motor cut";
                    break;
                case "dp motor cut":
                    orgName = "dpmotorcut";
                    testName = "dp motor cut";
                    break;
                case "sp com":
                    orgName = "spcom";
                    testName = "sp com";
                    break;
                case "dp com":
                    orgName = "dpcom";
                    testName = "dp com";
                    break;
                case "sp com + dp com":
                    orgName = "spdpcom";
                    testName = "sp com + dp com";
                    break;
                case "sp + tp":
                    orgName = "sptp";
                    testName = "sp + tp";
                    break;
                case "sp + dp":
                    orgName = "spdp";
                    testName = "sp + dp";
                    break;
                case "sp start":
                    orgName = "spstart";
                    testName = "sp start";
                    break;
                case "dp start":
                    orgName = "dpstart";
                    testName = "dp start";
                    break;
                case "sp end":
                    orgName = "spend";
                    testName = "sp end";
                    break;
                case "dp end":
                    orgName = "dpend";
                    testName = "dp end";
                    break;
                case "52 pana":
                    orgName = "52 pana";
                    testName = "52 pana";
                    break;
                case "56 pana":
                    orgName = "56 pana";
                    testName = "56 pana";
                    break;
                case "83 pana":
                    orgName = "83 pana";
                    testName = "83 pana";
                    break;
                case "64 pana":
                    orgName = "64 pana";
                    testName = "64 pana";
                    break;
                case "68 pana":
                    orgName = "68 pana";
                    testName = "68 pana";
                    break;
                case "72 pana":
                    orgName = "72 pana";
                    testName = "72 pana";
                    break;
                case "50 pana":
                    orgName = "50 pana";
                    testName = "50 pana";
                    break;
                case "chipke":
                    orgName = "chipke";
                    testName = "chipke";
                    break;
                case "bhikre":
                    orgName = "bhikre";
                    testName = "bhikre";
                    break;
                case "abr 30":
                    orgName = "abr30";
                    testName = "abr 30";
                    break;
                case "40 pana":
                    orgName = "pana40";
                    testName = "40 pana";
                    break;
                case "sabhi figure cut pana":
                    orgName = "cut40";
                    testName = "sabhi figure cut pana";
                    break;
                case "forgot pana":
                    orgName = "forgot pana";
                    testName = "forgot pana";
                    break;
                case "run pana":
                    orgName = "run pana";
                    testName = "run pana";
                    break;
                case "ko touch fail pana":
                    orgName = "ko touch fail pana";
                    testName = "ko touch fail pana";
                    break;
                case "family":
                    orgName = "family pana";
                    testName = "family";
                    break;
                case "abrcut 90 pana":
                    orgName = "Abr Cut 90 pana";
                    testName = "abrcut 90 pana";
                    break;
                case "centerpana":
                    orgName = "centerpana";
                    testName = "centerpana";
                    break;
                case "spdpmotorcut":
                    orgName = "sp motor cut + dp motor cut";
                    testName = "spdpmotorcut";
                    break;
                case "spdpstart":
                    orgName = "sp start + dp start";
                    testName = "spdpstart";
                    break;
                case "spdpend":
                    orgName = "sp end + dp end";
                    testName = "spdpend";
                    break;
                case "spdptp":
                    orgName = "sp + dp + tp";
                    testName = "spdptp";
                    break;
            }

            return (orgName,testName);
        }


    }

    #endregion

    #region Group Game Model

    public class GroupGameModel
    {
        private DatabaseHelper dbHelper;

        public GroupGameModel(DatabaseHelper helper)
        {
            this.dbHelper = helper;
        }

        // Helper method to execute query and return concatenated pana string
        private string ExecuteQueryGetPana(string sql, Dictionary<string, object> parameters = null)
        {
            string returnStr = "";
            try
            {
                var cmd = new SQLiteCommand(sql);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        cmd.Parameters.AddWithValue(param.Key, param.Value);
                    }
                }

                DataTable dt = dbHelper.Read(cmd);

                foreach (DataRow row in dt.Rows)
                {
                    if (row["li_pana"] != DBNull.Value && !string.IsNullOrEmpty(row["li_pana"].ToString()))
                    {
                        returnStr = returnStr + "," + row["li_pana"].ToString();
                    }
                }

                if (returnStr.StartsWith(","))
                {
                    returnStr = returnStr.TrimStart(',');
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Query error: {ex.Message}");
            }

            return returnStr;
        }

        #region Motor Functions

        public string GetMotorSp(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana
                           FROM li_pana
                           WHERE (INSTR(@number, SUBSTR(pana, 1, 1)) > 0)
                           AND (INSTR(@number, SUBSTR(pana, 2, 1)) > 0)
                           AND (INSTR(@number, SUBSTR(pana, 3, 1)) > 0)
                           AND pana_type = 1";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@number", number } });
        }

        public string GetMotorDp(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana
                           FROM li_pana
                           WHERE (INSTR(@number, SUBSTR(pana, 1, 1)) > 0)
                           AND (INSTR(@number, SUBSTR(pana, 2, 1)) > 0)
                           AND (INSTR(@number, SUBSTR(pana, 3, 1)) > 0)
                           AND pana_type = 2";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@number", number } });
        }

        public string GetMotorCutSp(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana
                           FROM li_pana
                           WHERE NOT (
                               (INSTR(@number, SUBSTR(pana, 1, 1)) > 0)
                               OR (INSTR(@number, SUBSTR(pana, 2, 1)) > 0)
                               OR (INSTR(@number, SUBSTR(pana, 3, 1)) > 0)
                           )
                           AND pana_type = 1";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@number", number } });
        }

        public string GetMotorCutDp(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana
                           FROM li_pana
                           WHERE NOT (
                               (INSTR(@number, SUBSTR(pana, 1, 1)) > 0)
                               OR (INSTR(@number, SUBSTR(pana, 2, 1)) > 0)
                               OR (INSTR(@number, SUBSTR(pana, 3, 1)) > 0)
                           )
                           AND pana_type = 2";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@number", number } });
        }

        public string GetMotorSpDp(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana
                           FROM li_pana
                           WHERE (INSTR(@number, SUBSTR(pana, 1, 1)) > 0)
                           AND (INSTR(@number, SUBSTR(pana, 2, 1)) > 0)
                           AND (INSTR(@number, SUBSTR(pana, 3, 1)) > 0)
                           AND (pana_type = 1 OR pana_type = 2)";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@number", number } });
        }

        public string GetMotorCutSpDp(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana
                           FROM li_pana
                           WHERE NOT (
                               (INSTR(@number, SUBSTR(pana, 1, 1)) > 0)
                               OR (INSTR(@number, SUBSTR(pana, 2, 1)) > 0)
                               OR (INSTR(@number, SUBSTR(pana, 3, 1)) > 0)
                           )
                           AND (pana_type = 1 OR pana_type = 2)";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@number", number } });
        }

        #endregion

        #region Common and Family Functions

        public string GetCommonSP(string number)
        {
            string returnStr = "";
            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND pana_type = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number[i]}%" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetCommonDP(string number)
        {
            string returnStr = "";
            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND pana_type = 2";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number[i]}%" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetCommonSPDP(string number)
        {
            string returnStr = "";
            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND (pana_type = 1 OR pana_type = 2)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number[i]}%" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetFamily(string number)
        {
            number = number.Trim();
            string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                           FROM li_pana 
                           WHERE check_family LIKE @pattern";

            return ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number}%" } });
        }

        #endregion

        #region Chipke, Bikhre, Run, Center, Forgot, Cycle Functions

        public string GetChipke(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND check_chipke_bikhre = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetBikhre(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND check_chipke_bikhre = 2";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetRun(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND check_run = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetCenter(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE check_center = @digit AND check_center != ''";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetForgot(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND check_forgot = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetCycle(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var combinations = Get2DigitCombinations(number);

            foreach (var combination in combinations)
            {
                char[] digits = combination.ToCharArray();
                string sql;

                if (digits[0] == digits[1])
                {
                    sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                            FROM li_pana 
                            WHERE pana LIKE @pattern";

                    var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{combination}%" } });
                    if (!string.IsNullOrEmpty(result))
                    {
                        returnStr = returnStr + "," + result;
                    }
                }
                else
                {
                    sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                            FROM li_pana 
                            WHERE pana LIKE @pattern1 AND pana LIKE @pattern2";

                    var result = ExecuteQueryGetPana(sql, new Dictionary<string, object>
                    {
                        { "@pattern1", $"%{digits[0]}%" },
                        { "@pattern2", $"%{digits[1]}%" }
                    });

                    if (!string.IsNullOrEmpty(result))
                    {
                        returnStr = returnStr + "," + result;
                    }
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        private List<string> Get2DigitCombinations(string number)
        {
            var combinations = new List<string>();

            for (int i = 0; i < number.Length; i++)
            {
                for (int j = 0; j < number.Length; j++)
                {
                    combinations.Add(number[i].ToString() + number[j].ToString());
                }
            }

            return combinations.Distinct().ToList();
        }

        #endregion

        #region Type Functions (SP, DP, TP)

        public string GetSp(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND pana_type = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetDp(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND pana_type = 2";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpDp(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND (pana_type = 1 OR pana_type = 2)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpTp(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND (pana_type = 1 OR pana_type = 3)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetDpTp(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND (pana_type = 2 OR pana_type = 3)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpDpTp(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE number_main = @digit AND (pana_type = 1 OR pana_type = 2 OR pana_type = 3)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@digit", number[i].ToString() } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpStart(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND pana_type = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"{number[i]}%" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetDpStart(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND pana_type = 2";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"{number[i]}%" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpEnd(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND pana_type = 1";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number[i]}" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetDpEnd(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND pana_type = 2";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number[i]}" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpDpStart(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND (pana_type = 1 OR pana_type = 2)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"{number[i]}%" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetSpDpEnd(string number)
        {
            string returnStr = "";
            number = number.Trim();

            for (int i = 0; i < number.Length; i++)
            {
                string sql = @"SELECT GROUP_CONCAT(pana, ',') As li_pana 
                               FROM li_pana 
                               WHERE pana LIKE @pattern AND (pana_type = 1 OR pana_type = 2)";

                var result = ExecuteQueryGetPana(sql, new Dictionary<string, object> { { "@pattern", $"%{number[i]}" } });
                if (!string.IsNullOrEmpty(result))
                {
                    returnStr = returnStr + "," + result;
                }
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        #endregion

        #region Pana Array Functions

        public string GetSabhiFigurCut(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var panacut_1 = new int[] { 146, 380, 489, 560, 227 };
            var panacut_2 = new int[] { 138, 156, 237, 570, 499 };
            var panacut_3 = new int[] { 238, 247, 490, 580, 166 };
            var panacut_4 = new int[] { 149, 167, 257, 590, 338 };
            var panacut_5 = new int[] { 168, 249, 267, 348, 500 };
            var panacut_6 = new int[] { 150, 169, 349, 358, 277 };
            var panacut_7 = new int[] { 160, 250, 278, 368, 449 };
            var panacut_8 = new int[] { 279, 350, 378, 459, 116 };
            var panacut_9 = new int[] { 126, 270, 450, 469, 388 };
            var panacut_10 = new int[] { 127, 136, 389, 479, 550 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(panacut_1); break;
                    case '2': pana.AddRange(panacut_2); break;
                    case '3': pana.AddRange(panacut_3); break;
                    case '4': pana.AddRange(panacut_4); break;
                    case '5': pana.AddRange(panacut_5); break;
                    case '6': pana.AddRange(panacut_6); break;
                    case '7': pana.AddRange(panacut_7); break;
                    case '8': pana.AddRange(panacut_8); break;
                    case '9': pana.AddRange(panacut_9); break;
                    case '0': pana.AddRange(panacut_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get52Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana52_1 = new int[] { 137, 579, 678, 146, 380 };
            var pana52_2 = new int[] { 246, 480, 345, 237, 570 };
            var pana52_3 = new int[] { 139, 157, 120, 238, 580 };
            var pana52_4 = new int[] { 248, 680, 789, 149, 257 };
            var pana52_5 = new int[] { 159, 357, 456, 258, 780, 230 };
            var pana52_6 = new int[] { 240, 268, 123, 169, 358 };
            var pana52_7 = new int[] { 179, 359, 890, 278, 250 };
            var pana52_8 = new int[] { 260, 468, 567, 378, 350 };
            var pana52_9 = new int[] { 135, 379, 234, 270, 469 };
            var pana52_10 = new int[] { 280, 460, 190, 235, 370, 578 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana52_1); break;
                    case '2': pana.AddRange(pana52_2); break;
                    case '3': pana.AddRange(pana52_3); break;
                    case '4': pana.AddRange(pana52_4); break;
                    case '5': pana.AddRange(pana52_5); break;
                    case '6': pana.AddRange(pana52_6); break;
                    case '7': pana.AddRange(pana52_7); break;
                    case '8': pana.AddRange(pana52_8); break;
                    case '9': pana.AddRange(pana52_9); break;
                    case '0': pana.AddRange(pana52_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get56Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana56_1 = new int[] { 236, 245, 489, 579, 678 };
            var pana56_2 = new int[] { 237, 246, 345, 589, 679 };
            var pana56_3 = new int[] { 238, 247, 256, 346, 689 };
            var pana56_4 = new int[] { 239, 248, 257, 347, 356, 789 };
            var pana56_5 = new int[] { 249, 258, 267, 348, 456, 357 };
            var pana56_6 = new int[] { 259, 268, 349, 358, 367, 457 };
            var pana56_7 = new int[] { 269, 278, 359, 368, 458, 467 };
            var pana56_8 = new int[] { 279, 369, 378, 459, 468, 567 };
            var pana56_9 = new int[] { 234, 289, 379, 469, 478, 568 };
            var pana56_10 = new int[] { 235, 389, 479, 569, 578 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana56_1); break;
                    case '2': pana.AddRange(pana56_2); break;
                    case '3': pana.AddRange(pana56_3); break;
                    case '4': pana.AddRange(pana56_4); break;
                    case '5': pana.AddRange(pana56_5); break;
                    case '6': pana.AddRange(pana56_6); break;
                    case '7': pana.AddRange(pana56_7); break;
                    case '8': pana.AddRange(pana56_8); break;
                    case '9': pana.AddRange(pana56_9); break;
                    case '0': pana.AddRange(pana56_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get83Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana83_1 = new int[] { 128, 137, 146, 236, 245, 290, 380, 470, 560 };
            var pana83_2 = new int[] { 138, 147, 156, 237, 246, 390, 480, 570 };
            var pana83_3 = new int[] { 148, 157, 238, 247, 256, 346, 490, 580, 670 };
            var pana83_4 = new int[] { 130, 158, 167, 248, 257, 347, 356, 590, 680 };
            var pana83_5 = new int[] { 140, 168, 230, 258, 267, 348, 357, 690, 780 };
            var pana83_6 = new int[] { 150, 178, 240, 268, 358, 367, 457, 790 };
            var pana83_7 = new int[] { 124, 160, 250, 278, 340, 368, 458, 467 };
            var pana83_8 = new int[] { 125, 134, 170, 260, 350, 378, 468 };
            var pana83_9 = new int[] { 126, 135, 180, 270, 360, 450, 478, 568 };
            var pana83_10 = new int[] { 127, 136, 145, 235, 280, 370, 460, 578 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana83_1); break;
                    case '2': pana.AddRange(pana83_2); break;
                    case '3': pana.AddRange(pana83_3); break;
                    case '4': pana.AddRange(pana83_4); break;
                    case '5': pana.AddRange(pana83_5); break;
                    case '6': pana.AddRange(pana83_6); break;
                    case '7': pana.AddRange(pana83_7); break;
                    case '8': pana.AddRange(pana83_8); break;
                    case '9': pana.AddRange(pana83_9); break;
                    case '0': pana.AddRange(pana83_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get64Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana64_1 = new int[] { 128, 137, 146, 290, 380, 470, 560 };
            var pana64_2 = new int[] { 129, 138, 147, 156, 390, 480, 570 };
            var pana64_3 = new int[] { 120, 139, 148, 157, 490, 580, 670 };
            var pana64_4 = new int[] { 130, 149, 158, 167, 590, 680 };
            var pana64_5 = new int[] { 140, 159, 168, 230, 690, 780 };
            var pana64_6 = new int[] { 123, 150, 169, 178, 240, 790 };
            var pana64_7 = new int[] { 124, 160, 179, 250, 340, 890 };
            var pana64_8 = new int[] { 125, 134, 170, 189, 260, 350 };
            var pana64_9 = new int[] { 126, 135, 180, 270, 360, 450 };
            var pana64_10 = new int[] { 127, 136, 145, 190, 280, 370, 460 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana64_1); break;
                    case '2': pana.AddRange(pana64_2); break;
                    case '3': pana.AddRange(pana64_3); break;
                    case '4': pana.AddRange(pana64_4); break;
                    case '5': pana.AddRange(pana64_5); break;
                    case '6': pana.AddRange(pana64_6); break;
                    case '7': pana.AddRange(pana64_7); break;
                    case '8': pana.AddRange(pana64_8); break;
                    case '9': pana.AddRange(pana64_9); break;
                    case '0': pana.AddRange(pana64_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get68Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana68_1 = new int[] { 128, 236, 245, 290, 560, 470, 489 };
            var pana68_2 = new int[] { 129, 138, 147, 156, 390, 589, 679 };
            var pana68_3 = new int[] { 148, 247, 256, 346, 490, 670, 689 };
            var pana68_4 = new int[] { 130, 158, 167, 239, 347, 356, 590 };
            var pana68_5 = new int[] { 140, 168, 249, 267, 690, 348 };
            var pana68_6 = new int[] { 150, 178, 259, 349, 367, 457, 790 };
            var pana68_7 = new int[] { 124, 160, 269, 340, 368, 458, 467 };
            var pana68_8 = new int[] { 125, 134, 170, 189, 279, 459, 369 };
            var pana68_9 = new int[] { 126, 180, 289, 360, 450, 568, 478 };
            var pana68_10 = new int[] { 127, 136, 145, 389, 479, 569 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana68_1); break;
                    case '2': pana.AddRange(pana68_2); break;
                    case '3': pana.AddRange(pana68_3); break;
                    case '4': pana.AddRange(pana68_4); break;
                    case '5': pana.AddRange(pana68_5); break;
                    case '6': pana.AddRange(pana68_6); break;
                    case '7': pana.AddRange(pana68_7); break;
                    case '8': pana.AddRange(pana68_8); break;
                    case '9': pana.AddRange(pana68_9); break;
                    case '0': pana.AddRange(pana68_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get72Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana72_1 = new int[] { 128, 146, 129, 138, 147, 156, 148, 130, 149, 158, 167, 140, 168, 150, 169, 178, 124, 160, 125, 134, 170, 189, 126, 180, 127, 136, 145 };
            var pana72_2 = new int[] { 128, 236, 245, 290, 129, 237, 238, 247, 256, 239, 257, 230, 249, 258, 267, 259, 124, 250, 269, 278, 125, 279, 126, 270, 289, 235, 127 };
            var pana72_3 = new int[] { 236, 380, 138, 237, 390, 238, 346, 130, 239, 347, 356, 230, 348, 349, 358, 367, 340, 368, 134, 350, 369, 378, 360, 136, 235, 370, 389 };
            var pana72_4 = new int[] { 146, 245, 470, 147, 148, 247, 346, 490, 149, 347, 140, 249, 348, 349, 457, 124, 340, 458, 467, 134, 459, 450, 469, 478, 145, 479, 489 };
            var pana72_5 = new int[] { 245, 560, 570, 156, 589, 256, 580, 158, 257, 356, 590, 258, 150, 259, 358, 457, 250, 458, 125, 350, 459, 450, 568, 145, 235, 569, 578 };
            var pana72_6 = new int[] { 146, 236, 560, 156, 679, 256, 346, 670, 689, 167, 356, 168, 267, 690, 169, 367, 160, 269, 368, 467, 369, 126, 360, 469, 568, 136, 569 };
            var pana72_7 = new int[] { 470, 147, 237, 570, 679, 247, 670, 167, 257, 347, 267, 780, 178, 367, 457, 790, 278, 467, 170, 279, 378, 270, 478, 127, 370, 479, 578 };
            var pana72_8 = new int[] { 128, 380, 489, 138, 589, 148, 238, 580, 689, 158, 168, 258, 348, 780, 178, 358, 278, 368, 458, 189, 378, 180, 289, 478, 568, 389, 578 };
            var pana72_9 = new int[] { 290, 489, 129, 390, 589, 679, 490, 689, 149, 239, 590, 249, 690, 169, 259, 349, 790, 269, 189, 279, 369, 459, 289, 469, 389, 479, 569 };
            var pana72_10 = new int[] { 290, 380, 470, 560, 390, 570, 490, 580, 670, 130, 590, 140, 230, 690, 780, 150, 790, 160, 250, 340, 170, 350, 180, 270, 360, 450, 370 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana72_1); break;
                    case '2': pana.AddRange(pana72_2); break;
                    case '3': pana.AddRange(pana72_3); break;
                    case '4': pana.AddRange(pana72_4); break;
                    case '5': pana.AddRange(pana72_5); break;
                    case '6': pana.AddRange(pana72_6); break;
                    case '7': pana.AddRange(pana72_7); break;
                    case '8': pana.AddRange(pana72_8); break;
                    case '9': pana.AddRange(pana72_9); break;
                    case '0': pana.AddRange(pana72_10); break;
                }
            }

            pana = pana.Distinct().ToList();

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string Get50Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana50_1 = new int[] { 290, 380, 470, 560, 100 };
            var pana50_2 = new int[] { 129, 138, 147, 156, 110 };
            var pana50_3 = new int[] { 120, 238, 247, 256, 229 };
            var pana50_4 = new int[] { 130, 239, 347, 356, 338 };
            var pana50_5 = new int[] { 140, 249, 348, 456, 447 };
            var pana50_6 = new int[] { 150, 259, 358, 457, 556 };
            var pana50_7 = new int[] { 160, 269, 368, 467, 566 };
            var pana50_8 = new int[] { 170, 279, 378, 567, 477 };
            var pana50_9 = new int[] { 180, 289, 478, 568, 388 };
            var pana50_10 = new int[] { 190, 389, 479, 569, 299 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana50_1); break;
                    case '2': pana.AddRange(pana50_2); break;
                    case '3': pana.AddRange(pana50_3); break;
                    case '4': pana.AddRange(pana50_4); break;
                    case '5': pana.AddRange(pana50_5); break;
                    case '6': pana.AddRange(pana50_6); break;
                    case '7': pana.AddRange(pana50_7); break;
                    case '8': pana.AddRange(pana50_8); break;
                    case '9': pana.AddRange(pana50_9); break;
                    case '0': pana.AddRange(pana50_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        public string GetAbr30Pana(string number)
        {
            string returnStr = "";
            number = number.Trim();

            var pana30_1 = new int[] { 678, 579, 137 };
            var pana30_2 = new int[] { 345, 246, 480 };
            var pana30_3 = new int[] { 120, 139, 157 };
            var pana30_4 = new int[] { 789, 680, 248 };
            var pana30_5 = new int[] { 456, 357, 159 };
            var pana30_6 = new int[] { 123, 240, 268 };
            var pana30_7 = new int[] { 890, 179, 359 };
            var pana30_8 = new int[] { 567, 468, 260 };
            var pana30_9 = new int[] { 234, 135, 379 };
            var pana30_10 = new int[] { 190, 280, 460 };

            var pana = new List<int>();

            for (int i = 0; i < number.Length; i++)
            {
                switch (number[i])
                {
                    case '1': pana.AddRange(pana30_1); break;
                    case '2': pana.AddRange(pana30_2); break;
                    case '3': pana.AddRange(pana30_3); break;
                    case '4': pana.AddRange(pana30_4); break;
                    case '5': pana.AddRange(pana30_5); break;
                    case '6': pana.AddRange(pana30_6); break;
                    case '7': pana.AddRange(pana30_7); break;
                    case '8': pana.AddRange(pana30_8); break;
                    case '9': pana.AddRange(pana30_9); break;
                    case '0': pana.AddRange(pana30_10); break;
                }
            }

            if (pana.Count > 0)
            {
                returnStr = string.Join(", ", pana);
            }

            if (returnStr.StartsWith(","))
            {
                returnStr = returnStr.TrimStart(',');
            }

            return returnStr;
        }

        #endregion
    }

    #endregion
}
