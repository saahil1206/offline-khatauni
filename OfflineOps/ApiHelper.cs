using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
    }
}