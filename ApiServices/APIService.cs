using Common;
using Common.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ApiServices
{
    public class APIResult
    {
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public dynamic ReturnData { get; set; }
    }

    public static class APIService
    {
        private static string token;
        private static string refreshToken;
        private static readonly string _email = AppConfig.GetStringValue("APIAdminUsername");
        private static readonly string _passWord = AppConfig.GetStringValue("APIAdminPassword");
        private static readonly string _hostName = AppConfig.GetStringValue("APIHostName");
        private static readonly string _mediaType = "application/json-patch+json";
        private static HttpClient _httpClient;
        
        static APIService()
        {
            _httpClient = new HttpClient();
            
        }
        static async Task<APIResult> PostRequest(string endPoint, object requestObject)
        {
            var result = new APIResult();
            try
            {
                string apiUrl = $"{_hostName}{endPoint}";
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                string jsonRequest = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
                var content = new StringContent(jsonRequest, null, _mediaType);
                request.Content = content;
                var response = await _httpClient.SendAsync(request);
                var res = response.Content.ReadAsStringAsync().Result;
                result.Message = res;
                result.ReturnData = res;
                result.IsSuccess = response.IsSuccessStatusCode;
                result.StatusCode = (int)response.StatusCode;

                return result;
            }
            catch (Exception ex)
            {
                return new APIResult
                {
                    Message = ex.Message,
                    StatusCode = 000,
                    IsSuccess = false,
                };
            }
        }

        static async Task<APIResult> GetRequest(string endPoint, object requestObject)
        {
            var result = new APIResult();
            try
            {
                string apiUrl = $"{_hostName}{endPoint}";
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                string jsonRequest = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
                var content = new StringContent(jsonRequest, null, _mediaType);
                request.Content = content;
                var response = await _httpClient.SendAsync(request);
                var res = response.Content.ReadAsStringAsync().Result;
                result.Message = res;
                result.ReturnData = res;
                result.IsSuccess = response.IsSuccessStatusCode;
                result.StatusCode = (int)response.StatusCode;

                return result;
            }
            catch (Exception ex)
            {
                return new APIResult
                {
                    Message = ex.Message,
                    StatusCode = 000,
                    IsSuccess = false,
                };
            }
        }

        public static async void GetToken()
        {
            while (true)
            {
                try
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://vidsto-api.uro-solution.info/api/Account/Login");
                    var requestObject = new Account
                    {
                        Email = _email,
                        Password = _passWord,
                        RememberMe = true,
                        ReturnUrl = _hostName,
                    };
                    string jsonRequest = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
                    var content = new StringContent(jsonRequest, null, _mediaType);
                    request.Content = content;
                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string resJson = response.Content.ReadAsStringAsync().Result;
                        JObject js = JObject.Parse(resJson);
                        token = js["content"]["token"].ToString();
                        refreshToken = js["content"]["refreshToken"].ToString();
                        MainLogger.Info("Get Token OK!");
                        break;
                    }
                    else
                    {
                        MainLogger.Error("Lỗi get token từ server");
                    }
                }
                catch (Exception ex)
                {
                    MainLogger.Error($"GetToken exception: {ex}");
                }
                finally { await Task.Delay(5000); }
            }
        }
        public static async Task<APIResult> CreateSession(string scannerCode, string userId, int deskId)
        {
            APIResult result = new APIResult();
            string failMsg = string.Empty;
            try
            {
                string endPoint = "api/cms/Desk/StartSession";
                string successMsg = $"MaNV = {userId} đăng ký làm tại bàn deskId={deskId} thành công!";
                failMsg = $"MaNV = {userId} đăng ký làm tại bàn deskId={deskId} thất bại!";

                var requestObject = new 
                {
                    ScannerCode = scannerCode,
                    UserId = userId,
                    DeskId = deskId
                };
                result = await PostRequest(endPoint, requestObject);
                if (result.IsSuccess)
                {
                    result.Message = (successMsg);
                }
                else
                {
                    result.Message = failMsg;
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(failMsg);
                MainLogger.Error("CreateSession exception " + ex);
                result.Message = ex.Message;
            }
            return result;
        }             
        public static async Task<APIResult> CreateOrder(string orderCode, int deskId)
        {
            APIResult result = new APIResult();
            string failMsg = string.Empty;
            try
            {
                string endPoint = "api/cms/Order/CreateNewOrder";
                string successMsg = ($"OrderCode = {orderCode}: Đăng ký đóng gói thành công!");
                failMsg = ($"OrderCode = {orderCode}: Đăng ký đóng gói thất bại!");

                var requestObject = new Order
                {
                    OrderCode = orderCode,
                    Start = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                    DeskId = deskId
                };
                result = await PostRequest(endPoint, requestObject);
                if (result.IsSuccess)
                {
                    result.Message = (successMsg);
                    var responseBody = result.ReturnData;
                    var data = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    if (data != null)
                    {
                        result.ReturnData = data?.content?.id?.ToString();
                    }
                }
                else
                {
                    result.Message = result.ReturnData;
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(failMsg);
                MainLogger.Error("CreateSession exception " + ex);
                result.Message = ex.Message;
            }
            return result;
        }
        public static async Task<APIResult> EndOrder(string orderId, string orderCode)
        {
            APIResult result = new APIResult();
            string failMsg = string.Empty;
            try
            {
                string endPoint = "api/cms/Order/EndOrder";
                string successMsg = $"Kết thúc đóng gói đơn {orderCode} thành công!";
                failMsg = $"Kết thúc đóng gói đơn {orderCode} thất bại!";

                var requestObject = new Order
                {
                    End = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                    OrderId = orderId
                };
                result = await PostRequest(endPoint, requestObject);
                if (result.IsSuccess)
                {
                    result.Message = (successMsg);
                    var responseBody = result.ReturnData;
                    var data = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    if (data != null)
                    {
                        result.ReturnData = data?.content?.id?.ToString();
                    }
                }
                else
                {
                    result.Message = failMsg;
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(failMsg);
                MainLogger.Error("CreateSession exception " + ex);
                result.Message = ex.Message;
            }
            return result;
        }
        public static async Task<APIResult> UploadVideo(Video videoUpload)
        {
            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://vidsto-api.uro-solution.info/api/cms/Video/Upload");
                var multipartContent = new MultipartFormDataContent();

                foreach (var videoPath in videoUpload.VideoPaths)
                {
                    if (File.Exists(videoPath))
                    {
                        try
                        {
                            byte[] videoBytes;
                            using (var fs = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                videoBytes = new byte[fs.Length];
                                fs.Read(videoBytes, 0, videoBytes.Length);
                            }
                            var videoContent = new ByteArrayContent(videoBytes);
                            videoContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(videoPath));
                            videoContent.Headers.ContentLength = videoBytes.Length;
                            multipartContent.Add(videoContent, "Videos", Path.GetFileName(videoPath));
                        }
                        catch (Exception ex)
                        {
                            MainLogger.Error($"Error processing file {videoPath}: {ex.Message}");
                            // Handle exception as needed
                        }
                    }
                    else
                    {
                        MainLogger.Error($"File not found: {videoPath}");
                    }
                }

                multipartContent.Add(new StringContent(videoUpload.OrderId), "OrderId");
                multipartContent.Add(new StringContent(videoUpload.CameraCode ?? ""), "CameraCode");
                request.Content = multipartContent;
                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    MainLogger.Info($"Videos uploaded successfully. OrderCode = {videoUpload.OrderCode}");
                }
                else
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    MainLogger.Error($"Failed to upload videos. Status code: {response.StatusCode}, Response: {responseContent}");
                }
                return new APIResult
                {
                    IsSuccess = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Message = response.ReasonPhrase
                };
            }
            catch (Exception ex)
            {
                MainLogger.Error(ex);
                return new APIResult() { Message = ex.ToString() };
            }
        }

        public static async Task<APIResult> UploadVideoNew(Video videoUpload)
        {
            APIResult result = new APIResult();
            string failMsg = $"Failed to upload videos for OrderCode {videoUpload.OrderCode}!";
            try
            {
                string endPoint = "api/cms/Video/Upload";
                string successMsg = $"Videos uploaded successfully for OrderCode {videoUpload.OrderCode}!";

                var multipartContent = new MultipartFormDataContent();

                foreach (var videoPath in videoUpload.VideoPaths)
                {
                    if (File.Exists(videoPath))
                    {
                        try
                        {
                            byte[] videoBytes;
                            using (var fs = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                videoBytes = new byte[fs.Length];
                                fs.Read(videoBytes, 0, videoBytes.Length);
                            }
                            var videoContent = new ByteArrayContent(videoBytes);
                            videoContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                            videoContent.Headers.ContentLength = videoBytes.Length;
                            multipartContent.Add(videoContent, "Videos", Path.GetFileName(videoPath));
                        }
                        catch (Exception ex)
                        {
                            MainLogger.Error($"Error processing file {videoPath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        MainLogger.Error($"File not found: {videoPath}");
                    }
                }

                multipartContent.Add(new StringContent(videoUpload.OrderId), "OrderId");
                multipartContent.Add(new StringContent(videoUpload.CameraCode ?? ""), "CameraCode");

                result = await PostRequest(endPoint, multipartContent);

                if (result.IsSuccess)
                {
                    var responseBody = result.ReturnData;
                    result.Message = successMsg;
                    MainLogger.Info(result.Message);
                    try
                    {
                        var data = JsonConvert.DeserializeObject<dynamic>(responseBody);
                        if (data != null)
                        {
                            result.ReturnData = data?.content?.id?.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error(ex);
                    }
                }
                else
                {
                    result.Message = failMsg;
                    MainLogger.Error(result.Message);
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(failMsg);
                MainLogger.Error("UploadVideo exception " + ex);
                result.Message = ex.Message;
            }

            return result;
        }
        private static string GetMimeType(string filePath)
        {
            switch (System.IO.Path.GetExtension(filePath).ToLower())
            {
                case ".mov":
                    return "video/quicktime";
                case ".mp4":
                    return "video/mp4";
                case ".ts":
                    return "video/mp2t";
                default:
                    throw new InvalidOperationException("Unsupported file type");
            }
        }

    }
}
