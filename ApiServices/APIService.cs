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
        public string ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public dynamic ReturnData { get; set; }
    }

    public static class APIService
    {
        private static string token;
        private static string refreshToken;
        private static string _hostName = AppConfig.GetStringValue("APIHostName");
        private static readonly string _email = AppConfig.GetStringValue("APIAdminUsername");
        private static readonly string _passWord = AppConfig.GetStringValue("APIAdminPassword");
        private static readonly string _mediaType = "application/json-patch+json";
        private static HttpClient _httpClient;

        static APIService()
        {
            _httpClient = new HttpClient();
        }

        public static bool CheckConnection()
        {
            try
            {
                return Login().Result.IsSuccess;
            }
            catch (Exception ex)
            {
                MainLogger.Error("CheckConnection ex");
                MainLogger.Error(ex);
                return false;
            }
        }

        public static async Task<APIResult> Login()
        {
            try
            {
                string endPoint = "api/Account/Login";
                var obj = new
                {
                    email = _email,
                    passWord = _passWord,
                    rememberMe = true,
                    returnUrl = ""
                };
                return await PostRequest(endPoint, obj);
            }
            catch (Exception ex)
            {
                MainLogger.Error("CreateSession exception " + ex);
                return new APIResult();
            }
        }

        public static async Task<APIResult> CreateSession(string scannerCode, string userId, int deskId)
        {
            APIResult result = new APIResult();
            try
            {
                string endPoint = "api/cms/Desk/StartSession";
                var requestObject = new
                {
                    ScannerCode = scannerCode,
                    UserId = userId,
                    DeskId = deskId
                };
                result = await PostRequest(endPoint, requestObject);
            }
            catch (Exception ex)
            {
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
                if (result.ReturnData.ToLower().Contains("số thứ tự này đã tồn tại"))
                {
                    MainLogger.Warn(result.ReturnData);
                    result.IsSuccess = true;
                }

                if (result.IsSuccess)
                {
                    result.Message = (successMsg);
                    var responseBody = result.ReturnData;
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
                if (result.Message.ToLower().Contains("không có đơn nào đang được đóng"))
                {
                    result.IsSuccess = true;
                }
                if (result.IsSuccess)
                {
                    var responseBody = result.ReturnData;
                    var data = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    if (data != null)
                    {
                        {
                            result.ReturnData = data?.content?.id?.ToString();
                        }
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
                if (_hostName.EndsWith("/"))
                {
                    _hostName = _hostName.TrimEnd('/');
                }
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_hostName}/api/cms/Video/Upload");
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
                            VideoLogger.Error($"Error processing file {videoPath}: {ex.Message}");
                            // Handle exception as needed
                        }
                    }
                    else
                    {
                        VideoLogger.Error($"File not found: {videoPath}");
                    }
                }

                multipartContent.Add(new StringContent(videoUpload.OrderId), "OrderId");
                multipartContent.Add(new StringContent(videoUpload.CameraCode ?? ""), "CameraCode");
                request.Content = multipartContent;
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    VideoLogger.Info($"OrderCode = {videoUpload.OrderCode}. {responseContent}");
                }
                else
                {
                    VideoLogger.Error(responseContent);
                    VideoLogger.Error($"Failed to upload videos. Status code: {response.StatusCode}, Response: {responseContent}");
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
                VideoLogger.Error(ex);
                return new APIResult() { Message = ex.ToString() };
            }
        }
        private static async Task<APIResult> PostRequest(string endPoint, object requestObject)
        {
            var result = new APIResult();
            try
            {
                if (_hostName.EndsWith("/"))
                {
                    _hostName = _hostName.TrimEnd('/');
                }
                string apiUrl = $"{_hostName}/{endPoint}";
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
                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = res;
                }
                return result;
            }
            catch (Exception ex)
            {
                MainLogger.Error("PostRequest ex");
                MainLogger.Error(ex.Message);
                MainLogger.Error(ex.InnerException);
                return new APIResult
                {
                    Message = ex.Message + ex.InnerException,
                    StatusCode = 000,
                    IsSuccess = false,
                };
            }
        }
        private static async Task<APIResult> GetRequest(string requestURL)
        {
            var result = new APIResult();
            try
            {
                var response = await _httpClient.GetAsync(requestURL);
                result.IsSuccess = response.IsSuccessStatusCode;
                result.StatusCode = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    var res = response.Content.ReadAsStringAsync().Result;
                    result.Message = res;
                    result.ReturnData = res;
                }
                else
                {
                    result.ErrorMessage = $"{response.RequestMessage} - {result.StatusCode} - {response.ReasonPhrase}";
                }
                return result;
            }
            catch (Exception ex)
            {
                MainLogger.Error("GetRequest ex");
                MainLogger.Error(ex.Message);
                MainLogger.Error(ex.InnerException);
                return new APIResult
                {
                    Message = ex.Message,
                    StatusCode = 000,
                    IsSuccess = false,
                };
            }
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
