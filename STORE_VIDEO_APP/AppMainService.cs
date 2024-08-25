using ApiServices;
using Common;
using Common.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace STORE_VIDEO_APP
{
    /// <summary>
    /// Application Main Services
    /// </summary>
    public partial class AppMainService
    {
        #region FIELDS, PROPERTIES
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

        readonly int delayError = AppConfig.GetIntValue("DelayError");
        #endregion

        #region CONSTRUCTOR
        public AppMainService()
        {
            MainLogger.Info($"================================================================================================================================");
            MainLogger.Info($"Start Main Service. Time {ServerTimeHelper.GetUnixTimeSeconds()}");
            InitializeScannerService();
            InitializePipeService();
            InitializeServerConnection();
            InitializeTestService();
        }

        #endregion

        #region INIT
        private void InitializeServerConnection()
        {
            try
            {
                var check = APIService.CheckConnection();
                if (check)
                {
                    MainLogger.Info($"Check connect to API server {AppConfig.APIHostName} => OK");
                }
                else
                {
                    MainLogger.Error($"Check connect to API server {AppConfig.APIHostName} => Failed");
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializeApiService ex");
                MainLogger.Error(ex);
            }
        }
        private void InitializeTestService()
        {
            try
            {
                if (AppConfig.IsTestEnviroment)
                {

                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(ex);
            }
        }
        #endregion

        #region ACTION PROCESS

        private readonly object _lock = new object(); // Một khóa chung cho toàn bộ bộ đệm

        private readonly ConcurrentDictionary<string, object> _locks = new ConcurrentDictionary<string, object>();
        private void ProcessBuffer(string portName)
        {
            // Sử dụng cùng một khóa như trong DataReceivedHandler
            var portLock = _locks.GetOrAdd(portName, new object());

            try
            {
                var dataBuffer = _dataBuffers[portName];
                lock (portLock) // Khóa cụ thể cho cổng
                {
                    int newLineIndex;
                    while ((newLineIndex = dataBuffer.ToString().IndexOf('\r')) >= 0)
                    {
                        var line = dataBuffer.ToString(0, newLineIndex);
                        dataBuffer.Remove(0, newLineIndex + 1); // Bỏ qua ký tự '\r'
                        string idMachine = GetIDFromPort(portName);
                        if (!string.IsNullOrEmpty(idMachine))
                        {
                            Task.Run(() => ProcessCode(idMachine, line)); // Xử lý dòng dữ liệu
                        }
                        else
                        {
                            MainLogger.Warn("Không đọc được thông tin máy quét");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("Xảy ra lỗi trong ProcessBuffer");
                MainLogger.Error(ex);
            }
        }
        public async Task ProcessCode(string scannerCode, string inputCode)
        {
            //MainLogger.Info($"ProcessCode({scannerCode},{inputCode.ToJson()})");
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

                    session.CurrentOrder = new Order
                    {
                        OrderCode = newOrderCode,
                        StartTime = ServerTimeHelper.GetUnixTimeSeconds(),
                        EndTime = ServerTimeHelper.GetUnixTimeSeconds(),
                        Note = "Tạo lúc " + ServerTimeHelper.GetUnixTimeSeconds(),
                        UserId = session.User.UserId,
                        Status = 0,
                    };
                    await CallApiStartOrder(session);
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
                        //kiểm tra xem có đang đóng đơn hàng nào không
                        if (currentSession.CurrentOrder != null)
                        {
                            MainLogger.Warn($"Không thể đăng ký phiên làm việc khác, đang có đơn hàng \"{currentSession.CurrentOrder.OrderCode}\" chưa đóng xong!");
                            return;
                            //không cho thực hiện hành động gì khác vì phiên này đang đóng hàng dở 
                        }
                        else
                        if (currentSession.User.UserId != userId)
                        {
                            // Cập nhật thông tin nếu khác
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
                //MainLogger.Info($"Kết thúc đóng gói hàng {qrData.OrderCode}");
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
                MainLogger.Info($"Kết thúc đóng gói hàng {session.CurrentOrder.OrderCode}");
                Order order = session.CurrentOrder;
                bool requestEndOrder = await CallApiEndOrder(order);

                //Call api kết thúc đơn thành công
                if (requestEndOrder)
                {
                    MainLogger.Info($"Kết thúc đóng gói hàng {session.CurrentOrder.OrderCode} thành công!");
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
        #endregion

        #region CALL API
        private async Task CallApiStartSession(Session session)
        {
            try
            {
                APIResult result = new APIResult();
                int retryCount = 0;
                string successMsg = $"MaNV = {session.User.UserId} đăng ký làm tại bàn deskId={session.User.DeskId} thành công!";
                string failMsg = $"MaNV = {session.User.UserId} đăng ký làm tại bàn deskId={session.User.DeskId} thất bại!";
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
                            MainLogger.Info($"CallApiStartSession =>" + result.Message);
                            MainLogger.Info(successMsg);
                            break;
                        }
                        else
                        {
                            //call api bi loi
                            MainLogger.Error($"CallApiStartSession =>" + result.Message);
                            MainLogger.Error(failMsg);
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
                            MainLogger.Info($"CallApiStartOrder {session.CurrentOrder.OrderCode} =>" + result.Message);
                            break;
                        }
                        else
                        {
                            //call api bi loi
                            MainLogger.Error($"CallApiStartOrder {session.CurrentOrder.OrderCode} =>" + result.ErrorMessage);

                        }
                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error("Lỗi gửi lệnh đăng ký đơn lên server!");
                        MainLogger.Error($"CallApiStartOrder Error " + ex);
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
                MainLogger.Error("CallApiStartOrder ex " + ex);
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
                order.EndTime = ServerTimeHelper.GetUnixTimeSeconds();
                APIResult requestEndOrder = new APIResult();
                int retryCount = 0;
                while (true)
                {
                    try
                    {
                        //CALL API
                        if (retryCount != 0)
                        {
                            MainLogger.Warn($"Thử lại lần {retryCount}, gửi lệnh kết thúc đơn lên server! OrderCode={order}");
                        }
                        requestEndOrder = APIService.EndOrder(order.OrderId, order.OrderCode).Result;
                        if (requestEndOrder.IsSuccess)
                        {
                            //call api ok
                            MainLogger.Info($"CallApiEndOrder {order.OrderCode} =>" + requestEndOrder.Message);
                            return true;
                        }
                        else
                        {
                            MainLogger.Error($"CallApiEndOrder {order.OrderCode} =>" + requestEndOrder.ErrorMessage);
                            return false;
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

        #endregion

        #region FUNCTION

        public QRData GetQRData(string input)
        {
            try
            {
                //MainLogger.Info($"GetQRData({input})");
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

        #endregion
    }
}
