using Common;
using Common.Model;
using H.Pipes;
using H.Pipes.Args;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace STORE_VIDEO_APP
{
    /// <summary>
    /// Pipe Services
    /// </summary>
    public partial class AppMainService
    {
        #region FIELDS, PROPERTIES
        private PipeClient<Session> _pipeClient;

        private readonly string _pipeServerName = AppConfig.GetStringValue("ServerPipeName") ?? "StoreVideo_PipeService";

        #endregion

        #region INIT
        private async void InitializePipeService()
        {
            try
            {
                // Khởi tạo PipeClient
                _pipeClient = new PipeClient<Session>(_pipeServerName);
                InitializeServerEvents(); // Khởi tạo sự kiện cho server (nếu có)

                // Kết nối với PipeServer
                await _pipeClient.ConnectAsync();

                // Giữ kết nối mở
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
            catch (IOException ioEx)
            {
                // Xử lý lỗi IO riêng biệt
                MainLogger.Error("IOException occurred while initializing pipe service.");
                MainLogger.Error(ioEx);
            }
            catch (Exception ex)
            {
                // Xử lý các lỗi khác
                MainLogger.Error("An error occurred while initializing pipe service.");
                MainLogger.Error(ex);
            }
            finally
            {
                // Đảm bảo tài nguyên được giải phóng (nếu cần)
                //_pipeClient?.Dispose();
            }
        }

        private void InitializeServerEvents()
        {
            try
            {
                _pipeClient.MessageReceived += (o, args) => MessageReceived(o, args);
                _pipeClient.Disconnected += (o, args) => DisconnectServer(o, args);
                _pipeClient.Connected += (o, args) => ConnectServer(o, args);
                _pipeClient.ExceptionOccurred += (o, args) => OnExceptionOccurred(args.Exception);
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializeServerEvents ex");
                MainLogger.Error(ex);
            }
        }
        #endregion

        #region EVENT
        private void DisconnectServer(object o, ConnectionEventArgs<Session> args)
        {
            MainLogger.Warn($"Disconnected from server {_pipeServerName}");
        }
        private void MessageReceived(object o, ConnectionMessageEventArgs<Session> args)
        {
            try
            {
                MainLogger.Info("MessageReceived: " + args.Message.ToString());
            }
            catch (Exception ex)
            {
                MainLogger.Error("MessageReceived ex");
                MainLogger.Error(ex);
            }
        }
        private void ConnectServer(object o, ConnectionEventArgs<Session> args)
        {
            try
            {
                MainLogger.Info($"Connected to Video Service => {_pipeServerName}");
            }
            catch (Exception ex)
            {
                MainLogger.Error("ConnectServer ex");
                MainLogger.Error(ex);
            }
        }
        private void OnExceptionOccurred(Exception exception)
        {
            try
            {
                MainLogger.Error("OnExceptionOccurred " + exception);
            }
            catch (Exception ex)
            {
                MainLogger.Error("OnExceptionOccurred ex");
                MainLogger.Error(ex);
            }
        }
        #endregion
    }
}
