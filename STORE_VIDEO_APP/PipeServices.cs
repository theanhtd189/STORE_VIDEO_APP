using Common;
using Common.Model;
using H.Pipes;
using H.Pipes.Args;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace STORE_VIDEO_APP
{
    /// <summary>
    /// Pipe Services
    /// </summary>
    public partial class AppMainService
    {
        private PipeClient<Session> _pipeClient;
        private readonly string _pipeServerName = AppConfig.GetStringValue("ServerPipeName")?? "StoreVideo_PipeService";

        private async void InitializePipeService()
        {
            try
            {
                _pipeClient = new PipeClient<Session>(_pipeServerName);
                InitializeServerEvents();
                await _pipeClient.ConnectAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializePipeService ex");
                MainLogger.Error(ex);
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
    }
}
