using ApiServices;
using Common;
using Common.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace STORE_VIDEO_APP
{
    public partial class AppMainService
    {
        /// <summary>
        /// Lệnh đăng ký phiên làm việc
        /// </summary>
        const string COMMAND_STARTSESSION = "STARTSESSION";

        /// <summary>
        /// Lệnh đăng ký đóng đơn hàng
        /// </summary>
        const string COMMAND_STARTORDER = "STARTORDER";

        /// <summary>
        /// Lệnh đăng ký kết thúc đơn hàng
        /// </summary>
        const string COMMAND_ENDORDER = "ENDORDER";

        private static Dictionary<string, Session> _listSession = new Dictionary<string, Session>();

        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        int delayError = AppConfig.GetIntValue("DelayError");
        public AppMainService()
        {
            MainLogger.Info("Start application - main service");
            InitializeScannerService();
            InitializePipeService();
            if (AppConfig.GetBooleanValue("EnableTest"))
            {
                InitializeTestService();
            }
        }

        private void InitializeTestService()
        {
            try
            {
                TestProgram();
            }
            catch (Exception ex)
            {
                MainLogger.Error(ex);
            }
        }

        public async void ProcessCode(string scannerCode, string inputCode)
        {
            MainLogger.Info($"ProcessCode({scannerCode},{inputCode.ToJson()})");
            try
            {
                QRData qrData = GetQRData(inputCode);
                string actionType = qrData.Command;
                switch (actionType)
                {
                    case COMMAND_STARTSESSION:
                        {
                            await ProcessStartSession(scannerCode, qrData);
                        }
                        break;
                    case COMMAND_STARTORDER:
                        {
                            await ProcessStartOrder(scannerCode, qrData);
                        }
                        break;
                    case COMMAND_ENDORDER:
                        {
                            await ProcessEndOrder(scannerCode, qrData);
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"ProcessCode({scannerCode},{inputCode})");
                MainLogger.Error(ex);
            }
        }

        /// <summary>
        /// Xử lý khi người dùng quét mã đăng ký phiên làm việc
        /// </summary>
        /// <param name="scannerCode"></param>
        /// <param name="qrData"></param>
        private async Task ProcessStartSession(string scannerCode, QRData qrData)
        {
            try
            {
                MainLogger.Info($"ProcessStartSession({scannerCode},{qrData.ToJson()})");
                if (qrData == null)
                {
                    return;
                }
                MainLogger.Info($"Đăng ký ca làm việc! MaNV = {qrData.UserId}");
                Session currentSession = null;
                string userId = qrData.UserId;
                int deskId = qrData.DeskId;
                List<Camera> listCamera = qrData.Cameras;

                //Nếu máy quét này đã quét đăng ký phiên làm việc trước đó rồi
                if (_listSession.ContainsKey(scannerCode))
                {
                    currentSession = _listSession[scannerCode];

                    if (currentSession != null)
                    {
                        // Cập nhật thông tin nếu khác
                        if (currentSession.User.UserId != userId)
                        {
                            currentSession.User = new User { UserId = userId, DeskId = deskId };
                            currentSession.Scanner = new Scanner { ScannerCode = scannerCode };
                            currentSession.Cameras = listCamera;
                        }
                    }
                }
                else
                {
                    //Tạo phiên làm việc mới
                    currentSession = new Session
                    {
                        Scanner = new Scanner { ScannerCode = scannerCode },
                        User = new User { UserId = userId, DeskId = deskId },
                        Cameras = listCamera,
                        CurrentOrder = null
                    };
                    _listSession.Add(scannerCode, currentSession);
                }
                
                await CallApiStartSession(currentSession);
            }
            catch (Exception ex)
            {
                MainLogger.Error($"CreateSession({scannerCode},{qrData})");
                MainLogger.Error(ex);
            }
        }

        private async Task CallApiStartSession(Session session)
        {
            try
            {
                APIResult result = new APIResult();
                int retryCount = 0;
                while (true)
                {
                    try
                    {
                        //CALL API
                        if (retryCount != 0)
                        {
                            MainLogger.Warn($"Thử lại lần {retryCount}, gửi lệnh đăng ký phiên làm việc lên server!");
                        }
                        result = APIService.CreateSession(session.Scanner.ScannerCode, session.User.UserId, session.User.DeskId).Result;
                        if (result.IsSuccess)
                        {
                            MainLogger.Info(result.Message);
                            break;
                        }
                        else
                        {
                            //call api bi loi
                            MainLogger.Error($"Error: {result.Message}");
                            await Task.Delay(delayError * 1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        
                        MainLogger.Error(ex);
                    }
                    finally
                    {
                        if (!result.IsSuccess)
                        {
                            retryCount++;
                            MainLogger.Error("Lỗi gửi lệnh đăng ký đơn lên server!");
                            await Task.Delay(delayError * 1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"CallApiStartSession Error: {ex}");
            }
        }

        private async Task ProcessStartOrder(string scannerCode, QRData qrData)
        {
            try
            {
                MainLogger.Info($"ProcessStartOrder({scannerCode},{qrData.ToJson()})");
                if (qrData == null)
                {
                    return;
                }
                string newOrderCode = qrData.OrderCode;
                if (_listSession.Keys.Contains(scannerCode))
                {
                    var session = _listSession[scannerCode];

                    //trước đó có đơn hàng đang đóng mà chưa quét mã kết thúc
                    if (session?.CurrentOrder != null)
                    {
                        
                        var isOrderCreated = session.CurrentOrder.OrderCode == newOrderCode;
                        if (isOrderCreated)
                        {
                            MainLogger.Warn("Đơn này đã quét từ trước rồi");
                            return;
                        }
                        else
                        {
                            //đơn hàng trước đó và đơn hàng vừa mới quét khác nhau
                            //=> kết thúc đơn cũ để bắt đầu đơn mới
                            await EndOrderSession(session);
                        }
                    }
                    
                    {
                        //khong co don hang nao dang dong goi
                        session.CurrentOrder = new Order
                        {
                            OrderCode = newOrderCode,
                            StartTime = DateTime.Now,
                            EndTime = DateTime.Now,
                            Note = "Tạo lúc " + DateTime.Now,
                            UserId = session.User.UserId,
                            Status = 0,
                        };
                        await CallApiStartOrder(session);
                    }
                }
                else
                {
                    MainLogger.Error("=> Bạn chưa quét mã đăng ký phiên làm việc!");
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"StartOrder({scannerCode},{qrData})");
                MainLogger.Error(ex);
            }
        }

        private async Task CallApiStartOrder(Session session)
        {
            try
            {
                MainLogger.Info("CallApiStartOrder");
                APIResult result = new APIResult();
                int retryCount = 0;
                while (true)
                {
                    try
                    {
                        //CALL API
                        if (retryCount != 0)
                        {
                            MainLogger.Error($"Retry call API StartOrder, {retryCount} times!");
                        }

                        result = await APIService.CreateOrder(session.CurrentOrder.OrderCode, session.User.DeskId);
                        if (result.IsSuccess)
                        {
                            session.CurrentOrder.OrderId = result.ReturnData;
                            MainLogger.Info(result.Message);
                            break;
                        }
                        else
                        {
                            //call api bi loi
                            MainLogger.Error(result.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error("Lỗi gửi lệnh đăng ký đơn lên server!");
                        MainLogger.Error($"CallApiStartOrder Error "+ex);
                    }
                    finally
                    {
                        if (!result.IsSuccess)
                        {
                            await Task.Delay(delayError * 1000);
                            retryCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("CallApiStartOrder ex "+ex);
            }
        }

        /// <summary>
        /// Xử lý hành động khi người dùng quét mã kết thúc đơn
        /// </summary>
        /// <param name="scannerCode"></param>
        /// <param name="qrData"></param>
        private async Task ProcessEndOrder(string scannerCode, QRData qrData)
        {
            try
            {
                MainLogger.Info($"ProcessEndOrder({scannerCode},{qrData.ToJson()})");
                if (qrData == null)
                {
                    return;
                }
                MainLogger.Info($"Kết thúc đóng gói hàng {qrData.OrderCode}");
                int retryAction = 0;
                while (true)
                {
                    try
                    {
                        if (_listSession.Keys.Contains(scannerCode))
                        {
                            var session = _listSession[scannerCode];

                            if (session == null)
                            {
                                //chua quet bat dau phien lam viec
                                MainLogger.Warn($"{scannerCode} Chưa quét mã đăng ký phiên làm việc!");
                                break;
                            }
                            else if (session.CurrentOrder == null)
                            {
                                //chua quet bat dau dong hang hoac chua kip lay thong tin don hang 
                                MainLogger.Warn("Không tìm thấy đơn nào đang được đóng!");
                                break;
                            }
                            else if (string.IsNullOrEmpty(session.CurrentOrder.OrderId))
                            {
                                retryAction++;
                            }
                            else
                            {
                                await EndOrderSession(session);
                                break;
                            }
                        }
                        else
                        {
                            MainLogger.Warn("Chua quet bat dau phien lam viec");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error($"EndOrder({scannerCode},{qrData})");
                        MainLogger.Error(ex);
                    }
                    finally
                    {
                        if (retryAction > 0)
                        {
                            await Task.Delay(delayError * 1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("EndOrder ex");
                MainLogger.Error(ex);
            }

        }

        /// <summary>
        /// Xử lý kết thúc đơn hàng 
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private async Task EndOrderSession(Session session)
        {
            try
            {
                Order order = session.CurrentOrder;
                bool requestEndOrder = await CallApiEndOrder(order);

                //Call api kết thúc đơn thành công
                if (requestEndOrder)
                {
                    try
                    {
                        //gửi thông tin đơn cần lấy video vào pipe service
                        Session copySession = (Session)session.Clone();
                        await _pipeClient.WriteAsync(copySession);

                        //Khi đơn đã kết thúc thì xóa thông tin đơn đó khỏi phiên đóng gói
                        session.CurrentOrder = null;
                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error("Error send data to pipe server");
                        MainLogger.Error(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(" ex");
                MainLogger.Error(ex);
            }
        }

        /// <summary>
        /// Call API kết thúc đơn
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private async Task<bool> CallApiEndOrder(Order order)
        {
            try
            {
                order.EndTime = DateTime.Now;
                APIResult requestEndOrder = new APIResult();
                int retryCount = 0;
                while (true)
                {
                    try
                    {
                        //CALL API
                        if (retryCount != 0)
                        {
                            MainLogger.Warn($"Thử lại lần {retryCount}, gửi lệnh kết thúc đơn lên server!");
                        }
                        requestEndOrder = APIService.EndOrder(order.OrderId, order.OrderCode).Result;
                        if (requestEndOrder.IsSuccess)
                        {
                            //call api ok
                            MainLogger.Info(requestEndOrder.Message);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error($"Error: {ex}");
                    }
                    finally
                    {
                        if (!requestEndOrder.IsSuccess)
                        {
                            retryCount++;
                           MainLogger.Error("Lỗi gửi lệnh kết thúc đơn lên server!");
                            await Task.Delay(delayError * 1000);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"CallApiEndOrder Error: {ex}");
                return false;
            }
        }

        public QRData GetQRData(string input)
        {
            try
            {
                MainLogger.Info($"GetQRData({input})");
                if (Function.IsValidQRJson(input))
                {
                    return JsonConvert.DeserializeObject<QRData>(input);
                }
                else
                {
                    return new QRData()
                    {
                        Command = COMMAND_STARTORDER,
                        OrderCode = input,
                    };
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(ex);
                return null;
            }
        }

        public void TestProgram()
        {
            try
            {
                string jsonNv11 = "{\"UserId\":\"8171e130-6a3c-4d8e-9167-ddcb52672abc\",\"DeskId\":6,\"Command\":\"STARTSESSION\",\"Cameras\":[{\"Name\":\"Camera 01 - Kho 01\",\"Code\":\"CAMERA_BAN01_KHO01\",\"CameraIP\":\"192.168.1.168:8000\",\"CameraChannel\":\"47\",\"Id\":20}]}";

                string jsonEnd1 = "{\"DeskId\":6,\"Command\":\"ENDORDER\"}";
                string jsonEnd2 = "{\"DeskId\":7,\"Command\":\"ENDORDER\"}";
                string jsonEnd3 = "{\"DeskId\":8,\"Command\":\"ENDORDER\"}";

                string device1 = "SCN1";
                string device3 = "SCN3";
                string device2 = "SCN2";

                string jsonNv12 = "{\"UserId\":\"67347a29-cede-4862-bfe0-b2ddaccae518\",\"DeskId\":7,\"Command\":\"STARTSESSION\",\"Cameras\":[{\"Name\":\"Camera 02 - Kho 01\",\"Code\":\"CAMERA_BAN02_KHO01\",\"CameraIP\":\"192.168.1.168:8000\",\"CameraChannel\":\"45\",\"Id\":19}]}";
                string jsonNv13 = "{\"UserId\":\"c3a7b176-0689-4023-a268-ca0638d97af8\",\"DeskId\":8,\"Command\":\"STARTSESSION\",\"Cameras\":[{\"Name\":\"Camera 03 - Kho 01\",\"Code\":\"CAMERA_BAN03_KHO01\",\"CameraIP\":\"192.168.1.168:8000\",\"CameraChannel\":\"46\",\"Id\":18}]}";
                

                int i = 0;

                //{
                //    string jsonStart1 = $"ORDTEST{i++}-" + DateTime.Now.ToString("ddMMyy-HHmmss");
                //    string jsonStart2 = $"ORDTEST{i}-" + DateTime.Now.ToString("ddMMyy-HHmmss");

                Task.Run(() =>
                {
                    ProcessCode(device1, jsonNv11);

                    ProcessCode(device1, "donhang1");
                    ProcessCode(device1, jsonEnd1);
                });

                //Task.Run(() =>
                //{
                //    ProcessCode(device2, jsonNv12);
                //    ProcessCode(device2, "donhang2");
                //    ProcessCode(device1, jsonEnd2);
                //});                
                
                //Task.Run(() =>
                //{
                //    ProcessCode(device3, jsonNv13);
                //    ProcessCode(device3, "donhang3");
                //    ProcessCode(device1, jsonEnd3);
                //});

                //Thread.Sleep(5*60000);           
            }
            catch (Exception ex)
            {
                MainLogger.Error(" ex");
                MainLogger.Error(ex);
            }
        }
    }

}
