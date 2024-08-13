using Common;
using Common.Model;
using H.Pipes;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoServices
{
    public partial class VideoMainService
    {
        /// <summary>
        /// Khởi tạo các sự kiện cho Pipe service
        /// </summary>
        public static async void InitializeEvent()
        {
            try
            {
                await using var server = new PipeServer<Session>(_serverPipeName);
                server.ClientConnected += (o, args) =>
                {
                    VideoLogger.Info($"Connected to main service!");
                };
                server.ClientDisconnected += Server_ClientDisconnected;

                server.MessageReceived += Server_MessageReceived;

                server.ExceptionOccurred += (o, args) => OnExceptionOccurred(args.Exception);
            
                await server.StartAsync();

                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                VideoLogger.Error("InitializeEvent ex");
                VideoLogger.Error(ex);
            }

        }

        private static void Server_ClientDisconnected(object sender, H.Pipes.Args.ConnectionEventArgs<Session> args)
        {
            try
            {
                VideoLogger.Info($"Disconnected to {args.Connection.PipeName}");

            }
            catch (Exception ex)
            {
                VideoLogger.Error(ex);
            }
        }

        private static void Server_MessageReceived(object sender, H.Pipes.Args.ConnectionMessageEventArgs<Session> args)
        {
            try
            {
                VideoLogger.Info($"Received data => OrderCode={args.Message.CurrentOrder.OrderCode}");
                _queueMain.Enqueue(args.Message);
            }
            catch (Exception ex)   
            {
                VideoLogger.Error(ex);
            }
        }

        private static void OnExceptionOccurred(Exception exception)
        {
            VideoLogger.Error("OnExceptionOccurred: " + exception);
        }
    }
}
