using ApiServices;
using Common;
using Common.Model;
using H.Pipes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CameraServices
{

    public partial class Main : Form
    {
        private readonly string m_ip = AppConfig.GetStringValue("CameraAdminIP");
        private readonly Int16 m_port = (short)AppConfig.GetIntValue("CameraAdminPort");
        private readonly string m_userName = AppConfig.GetStringValue("CameraAdminUsername");
        private readonly string m_passWord = AppConfig.GetStringValue("CameraAdminPassword");
        private readonly string _videoOutputExtension = AppConfig.GetStringValue("VideoOutputExtension");
        private readonly string _videoResizeOutputExtension = AppConfig.GetStringValue("VideoResizeOutputExtension");
        private string m_code = AppConfig.GetStringValue("CameraCodeDefault");

        private string str;
        private string str1;
        private string str2;
        private Int32 i = 0;
        private Int32 m_lTree = 0;
        private Int32 m_lUserID = -1;
        private Int32 m_lDownHandle = -1;

        private bool m_bInitSDK = false;

        private long iSelIndex = 0;
        private uint iLastErr = 0;
        private uint dwAChanTotalNum = 0;
        private uint dwDChanTotalNum = 0;
        private uint m_channel;
        private int[] iChannelNum;

        public CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo;
        public CHCNetSDK.NET_DVR_IPPARACFG_V40 m_struIpParaCfgV40;
        public CHCNetSDK.NET_DVR_GET_STREAM_UNION m_unionGetStream;
        public CHCNetSDK.NET_DVR_IPCHANINFO m_struChanInfo;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 96, ArraySubType = UnmanagedType.U4)]

        /// <summary>
        /// Số giây (s) delay mỗi khi chạy 1 job 
        /// </summary>
        private static readonly int _delayJobTime = AppConfig.GetIntValue("DelayJobTime");

        /// <summary>
        /// Số giây (s) delay để thực hiện lại job khi job đó bị lỗi 
        /// </summary>
        private static readonly int _delayErrorJobTime = AppConfig.GetIntValue("DelayErrorJobTime");

        /// <summary>
        /// Danh sách các job cần được thực hiện
        /// </summary>
        private static Queue<Session> _queueMain = new Queue<Session>();

        /// <summary>
        /// Có xóa file gốc sau khi upload thành công không
        /// </summary>
        private bool _isDeleteOFileAfterUploaded = AppConfig.GetBooleanValue("IsDeleteOriginalFileAfterUploaded");

        /// <summary>
        /// Có xóa file đã nén sau khi upload thành công không
        /// </summary>
        private bool _isDeleteRFileAfterUploaded = AppConfig.GetBooleanValue("IsDeleteResizedFileAfterUploaded");

        /// <summary>
        /// Tên của Pipe Service đích 
        /// </summary>
        private static readonly string _serverPipeName = AppConfig.GetStringValue("ServerPipeName") ?? "StoreVideo_PipeService";

        #region EVENTS

        /// <summary>
        /// Khởi tạo các sự kiện cho Pipe service
        /// </summary>
        private async void InitializePipeServiceEvent()
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

        private void Server_ClientDisconnected(object sender, H.Pipes.Args.ConnectionEventArgs<Session> args)
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

        private void Server_MessageReceived(object sender, H.Pipes.Args.ConnectionMessageEventArgs<Session> args)
        {
            try
            {
                VideoLogger.Warn($"Received data => OrderCode = {args.Message.CurrentOrder.OrderCode}");
                _queueMain.Enqueue(args.Message);
            }
            catch (Exception ex)
            {
                VideoLogger.Error(ex);
            }
        }

        private void OnExceptionOccurred(Exception exception)
        {
            VideoLogger.Error("OnExceptionOccurred: " + exception);
        }

        private void timerProgress_Tick(object sender, EventArgs e)
        {
            DownloadProgressBar.Maximum = 100;
            DownloadProgressBar.Minimum = 0;

            int iPos = 0;

            //Get downloading process
            iPos = CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle);

            if ((iPos > DownloadProgressBar.Minimum) && (iPos < DownloadProgressBar.Maximum))
            {
                DownloadProgressBar.Value = iPos;
            }

            if (iPos == 100)  //Finish downloading
            {
                DownloadProgressBar.Value = iPos;
                if (!CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle))
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    str = "NET_DVR_StopGetFile failed, error code= " + iLastErr; //Download controlling failed,print error code
                    VideoLogger.Error(str);
                    return;
                }
                m_lDownHandle = -1;
                timerDownload.Stop();
                VideoLogger.Info("Download ok");
            }

            if (iPos == 200) //Network abnormal,download failed
            {
                VideoLogger.Error("The downloading is abnormal for the abnormal network!");
                timerDownload.Stop();
            }
        }

        private void btnDownloadTime_Click(object sender, EventArgs e)
        {
            DownloadOriginalVideo(dateTimeStart.Value, dateTimeEnd.Value);
        }

        private void btn_Exit_Click(object sender, EventArgs e)
        {
            //Stop download
            if (m_lDownHandle >= 0)
            {
                CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle);
                m_lDownHandle = -1;
            }

            //Logout the device
            if (m_lUserID >= 0)
            {
                CHCNetSDK.NET_DVR_Logout(m_lUserID);
                m_lUserID = -1;
            }
            Application.Exit();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            LoginCamera();
        }

        private void listViewIPChannel_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listViewIPChannel.SelectedItems.Count > 0)
            {
                iSelIndex = listViewIPChannel.SelectedItems[0].Index;  //Select the current items
                m_channel = 36;
            }
        }

        #endregion

        public Main()
        {
            VideoLogger.Info($"================================================================================================================================");
            VideoLogger.Info("Start video service");
            InitializeComponent();
            InitializeSystemInfo();
            InitializePipeServiceEvent();
            Task.Run(() =>
            {
                ProcessDataInQueueAsync();
            });
        }

        private void InitializeSystemInfo()
        {
            m_bInitSDK = CHCNetSDK.NET_DVR_Init();
            if (m_bInitSDK == false)
            {
                VideoLogger.Info("NET_DVR_Init error!");
                return;
            }
            else
            {
                //Save log of SDK
                CHCNetSDK.NET_DVR_SetLogToFile(3, "C:\\SdkLog\\", true);
                iChannelNum = new int[96];
            }
            LoginCamera();
        }

        private bool LoginCamera()
        {
            if (m_lUserID < 0)
            {
                string DVRIPAddress = m_ip; //IP or domain of device
                Int16 DVRPortNumber = m_port;//Service port of device
                string DVRUserName = m_userName;//Login name of deivce
                string DVRPassword = m_passWord;//Login password of device

                //Login the device
                m_lUserID = CHCNetSDK.NET_DVR_Login_V30(DVRIPAddress, DVRPortNumber, DVRUserName, DVRPassword, ref DeviceInfo);
                if (m_lUserID < 0)
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    str1 = "NET_DVR_Login_V30 failed, error code= " + iLastErr; //Login failed,print error code
                    VideoLogger.Error($"Camera {m_ip}:{m_port} ==> Login Failed!");
                    VideoLogger.Error(str1);
                    return false;
                }
                else
                {
                    //Login successsfully
                    VideoLogger.Info($"Camera {m_ip}:{m_port} ==> Login Succesfully!");
                    dwAChanTotalNum = (uint)DeviceInfo.byChanNum;
                    dwDChanTotalNum = (uint)DeviceInfo.byIPChanNum + 256 * (uint)DeviceInfo.byHighDChanNum;

                    if (dwDChanTotalNum > 0)
                    {
                        InfoIPChannel();
                    }
                    else
                    {
                        for (i = 0; i < dwAChanTotalNum; i++)
                        {
                            ListAnalogChannel(i + 1, 1);
                            iChannelNum[i] = i + (int)DeviceInfo.byStartChan;
                        }
                        // VideoLogger.Info("This device has no IP channel!");
                    }

                    return true;
                }
            }
            return false;
        }

        private void InfoIPChannel()
        {
            uint dwSize = (uint)Marshal.SizeOf(m_struIpParaCfgV40);

            IntPtr ptrIpParaCfgV40 = Marshal.AllocHGlobal((Int32)dwSize);
            Marshal.StructureToPtr(m_struIpParaCfgV40, ptrIpParaCfgV40, false);

            uint dwReturn = 0;
            int iGroupNo = 0; //The demo just acquire 64 channels of first group.If ip channels of device is more than 64,you should call NET_DVR_GET_IPPARACFG_V40 times to acquire more according to group 0~i
            if (!CHCNetSDK.NET_DVR_GetDVRConfig(m_lUserID, CHCNetSDK.NET_DVR_GET_IPPARACFG_V40, iGroupNo, ptrIpParaCfgV40, dwSize, ref dwReturn))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str1 = "NET_DVR_GET_IPPARACFG_V40 failed, error code= " + iLastErr; //Get IP parameter of configuration failed,print error code.
                VideoLogger.Info(str1);
            }
            else
            {
                // succ
                m_struIpParaCfgV40 = (CHCNetSDK.NET_DVR_IPPARACFG_V40)Marshal.PtrToStructure(ptrIpParaCfgV40, typeof(CHCNetSDK.NET_DVR_IPPARACFG_V40));

                for (i = 0; i < dwAChanTotalNum; i++)
                {
                    ListAnalogChannel(i + 1, m_struIpParaCfgV40.byAnalogChanEnable[i]);
                    iChannelNum[i] = i + (int)DeviceInfo.byStartChan;
                }

                byte byStreamType;
                uint iDChanNum = 64;

                if (dwDChanTotalNum < 64)
                {
                    iDChanNum = dwDChanTotalNum; //If the ip channels of device is less than 64,will get the real channel of device
                }

                for (i = 0; i < iDChanNum; i++)
                {
                    iChannelNum[i + dwAChanTotalNum] = i + (int)m_struIpParaCfgV40.dwStartDChan;

                    byStreamType = m_struIpParaCfgV40.struStreamMode[i].byGetStreamType;
                    m_unionGetStream = m_struIpParaCfgV40.struStreamMode[i].uGetStream;

                    switch (byStreamType)
                    {
                        //At present NVR just support case 0-one way to get stream from device
                        case 0:
                            dwSize = (uint)Marshal.SizeOf(m_unionGetStream);
                            IntPtr ptrChanInfo = Marshal.AllocHGlobal((Int32)dwSize);
                            Marshal.StructureToPtr(m_unionGetStream, ptrChanInfo, false);
                            m_struChanInfo = (CHCNetSDK.NET_DVR_IPCHANINFO)Marshal.PtrToStructure(ptrChanInfo, typeof(CHCNetSDK.NET_DVR_IPCHANINFO));

                            //List ip channels
                            ListIPChannel(i + 1, m_struChanInfo.byEnable, m_struChanInfo.byIPID);
                            Marshal.FreeHGlobal(ptrChanInfo);
                            break;

                        default:
                            break;
                    }
                }
            }
            Marshal.FreeHGlobal(ptrIpParaCfgV40);
        }

        private void ListIPChannel(Int32 iChanNo, byte byOnline, byte byIPID)
        {
            str1 = String.Format("IPCamera {0}", iChanNo);
            m_lTree++;

            if (byIPID == 0)
            {
                str2 = "X"; //The ip channel is empty,no front-end(such as camera)is added               
            }
            else
            {
                if (byOnline == 0)
                {
                    str2 = "offline"; //The channel is offline
                }
                else
                    str2 = "online"; //The channel is online
            }

            listViewIPChannel.Items.Add(new ListViewItem(new string[] { str1, str2 }));//Add channels to list
        }

        private void ListAnalogChannel(Int32 iChanNo, byte byEnable)
        {
            str1 = String.Format("Camera {0}", iChanNo);
            m_lTree++;

            if (byEnable == 0)
            {
                str2 = "Disabled"; //This channel has been disabled               
            }
            else
            {
                str2 = "Enabled"; //This channel has been enabled  
            }

            listViewIPChannel.Items.Add(new ListViewItem(new string[] { str1, str2 }));//Add channels to list
        }

        private string DownloadOriginalVideo(DateTime start, DateTime end)
        {
            VideoLogger.Info($"DownloadVideo({start}, {end}). Total = {(end - start).TotalMinutes} minutes");
            if (m_lDownHandle >= 0)
            {
                VideoLogger.Error("Downloading, please stop firstly!");//Please stop downloading
                return "";
            }

            CHCNetSDK.NET_DVR_PLAYCOND struDownPara = new CHCNetSDK.NET_DVR_PLAYCOND();
            struDownPara.dwChannel = (uint)m_channel; //Channel number  

            //Set the starting time
            struDownPara.struStartTime.dwYear = (uint)start.Year;
            struDownPara.struStartTime.dwMonth = (uint)start.Month;
            struDownPara.struStartTime.dwDay = (uint)start.Day;
            struDownPara.struStartTime.dwHour = (uint)start.Hour;
            struDownPara.struStartTime.dwMinute = (uint)start.Minute;
            struDownPara.struStartTime.dwSecond = (uint)start.Second;

            //Set the stopping time
            struDownPara.struStopTime.dwYear = (uint)end.Year;
            struDownPara.struStopTime.dwMonth = (uint)end.Month;
            struDownPara.struStopTime.dwDay = (uint)end.Day;
            struDownPara.struStopTime.dwHour = (uint)end.Hour;
            struDownPara.struStopTime.dwMinute = (uint)end.Minute;
            struDownPara.struStopTime.dwSecond = (uint)end.Second;

            string videoFileName = $"{start:ddMMyyyy-HHmmss}-{end:HHmmss-}{struDownPara.dwChannel}{_videoOutputExtension}";
            string videoFolderPath = Path.Combine(AppConfig.GetStringValue("StoreVideoPath"), m_code);
            string fullFilePath = Path.Combine(videoFolderPath, videoFileName);

            if (!Directory.Exists(videoFolderPath))
            {
                Directory.CreateDirectory(videoFolderPath);
                VideoLogger.Info($"Created path: {videoFolderPath}");
            }

            //Download by time
            m_lDownHandle = CHCNetSDK.NET_DVR_GetFileByTime_V40(m_lUserID, fullFilePath, ref struDownPara);

            if (m_lDownHandle < 0)
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_GetFileByTime_V40 failed, error code= " + iLastErr;
                VideoLogger.Error(str);
                return "";
            }

            uint iOutValue = 0;
            if (!CHCNetSDK.NET_DVR_PlayBackControl_V40(m_lDownHandle, CHCNetSDK.NET_DVR_PLAYSTART, IntPtr.Zero, 0, IntPtr.Zero, ref iOutValue))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_PLAYSTART failed, error code= " + iLastErr; //Download controlling failed,print error code
                VideoLogger.Error(str);
                return "";
            }

            var delaySecondDownload = (int)Math.Ceiling((end - start).TotalMinutes * 2);

            VideoLogger.Warn($"Wait {delaySecondDownload} seconds to download video");
            Thread.Sleep(delaySecondDownload * 1000);

            if (CheckStreamStatus())
            {
                if (!string.IsNullOrEmpty(fullFilePath) && File.Exists(fullFilePath))
                {
                    var size = new FileInfo(fullFilePath).Length;
                    VideoLogger.Info($"Video {fullFilePath}. File size = {size} bytes => Download OK");
                    return fullFilePath;
                }
            }
            return "";
        }

        private string GetResizedVideoPath(string originalVideoPath)
        {
            string result = "";
            try
            {
                if (!string.IsNullOrEmpty(originalVideoPath) && File.Exists(originalVideoPath))
                {
                    long sizeFile = new FileInfo(originalVideoPath).Length;
                    if (sizeFile > 0)
                    {
                        string outputFilePath = originalVideoPath.Replace(_videoOutputExtension, _videoResizeOutputExtension);
                        string cmd = string.Format("-y -i \"{0}\" -ar 22050 -ab 512 -b 800k -f mp4 -s 1920*1080 -strict -2 -c:a aac \"{1}\"", originalVideoPath, outputFilePath);
                        try
                        {
                            VideoLogger.Info($"Start formatting file ...");
                            ExecuteFFmpegCommand(cmd, out string duration);
                            if (File.Exists(outputFilePath) && new FileInfo(outputFilePath).Length > 0)
                            {
                                VideoLogger.Info($"Format file {outputFilePath} => OK");
                                return outputFilePath;
                            }
                        }
                        catch (Exception ex)
                        {
                            VideoLogger.Error(ex);
                            Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        VideoLogger.Error($"File {originalVideoPath} render failed!");
                    }
                }
            }
            catch (Exception ex)
            {
                VideoLogger.Error(ex);
            }

            return result;
        }

        private void ExecuteFFmpegCommand(string arguments, out string duration)
        {
            duration = string.Empty;
            try
            {
                Process ffmpeg = new Process();
                ffmpeg.StartInfo.FileName = "ffmpeg";
                ffmpeg.StartInfo.Arguments = $"{arguments}";
                ffmpeg.StartInfo.UseShellExecute = false;
                //ffmpeg.StartInfo.RedirectStandardOutput = true;
                //ffmpeg.StartInfo.RedirectStandardError = true;
                ffmpeg.StartInfo.CreateNoWindow = false;
                ffmpeg.Start();
                //string output = ffmpeg.StandardOutput.ReadToEnd();
                //string error = ffmpeg.StandardError.ReadToEnd();
                ffmpeg.WaitForExit();
                //duration = ParseDuration(error);
            }
            catch (Exception ex)
            {
                VideoLogger.Info(ex);
            }
        }

        private bool CheckStreamStatus()
        {
            int maxAttempts = 10;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                attempts++;
                int iPos = CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle);
                Thread.Sleep(maxAttempts * 1000);

                if (DownloadProgressBar.InvokeRequired)
                {
                    DownloadProgressBar.Invoke(new Action(() =>
                    {
                        DownloadProgressBar.Value = iPos;
                    }));
                }
                else
                {
                    DownloadProgressBar.Value = iPos;
                }

                if (iPos > 0 && iPos < 100)
                {
                    VideoLogger.Info($"Downloading... {DownloadProgressBar.Value}%");
                }
                else if (iPos == 100)
                {
                    // Finish downloading
                    if (!CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle))
                    {
                        iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                        str = "NET_DVR_StopGetFile failed, error code= " + iLastErr;
                        VideoLogger.Error(str);
                        return false;
                    }
                    m_lDownHandle = -1;
                    return true;
                }
                else if (iPos == 200) // Network abnormal, download failed
                {
                    VideoLogger.Error("The downloading is abnormal due to the abnormal network!");
                    return false;
                }
                else
                {
                    VideoLogger.Error($"iPos = {iPos}");
                    return false;
                }
            }

            VideoLogger.Error("Download timed out.");
            return false;
        }

        private async void ProcessDataInQueueAsync()
        {
            bool isError;
            bool isProcessingVideo = false;
            while (true)
            {
                try
                {
                    while (_queueMain.Count > 0 && isProcessingVideo == false)
                    {
                        isError = false;
                        isProcessingVideo = true;
                        Session session = _queueMain.Dequeue();
                        Order order = session?.CurrentOrder;
                        if (order != null)
                        {
                            try
                            {
                                foreach (Camera camera in session.Cameras)
                                {
                                    if (camera != null)
                                    {
                                        m_code = camera.Code;
                                        m_channel = (uint)camera.CameraChannel;

                                        //Video model để upload api
                                        Video videoUpload = new Video
                                        {
                                            Code = $"vid_{order.OrderCode}",
                                            OrderCode = order.OrderCode,
                                            OrderId = order.OrderId,
                                            StartTime = order.StartTime,
                                            EndTime = order.EndTime,
                                            CameraCode = camera.Code,
                                            ListCamera = session.Cameras,
                                            VideoPaths = new List<string>(),
                                        };

                                        string videoPath = GetPathForVideoUpload(order.StartTime, order.EndTime);
                                        if (!string.IsNullOrEmpty(videoPath))
                                        {
                                            videoUpload.VideoPaths.Add(videoPath);
                                            //Call API upload
                                            bool uploadVideo = await CallApiUploadVideoAsync(videoUpload);
                                            if (uploadVideo)
                                            {
                                                VideoLogger.Info($"OrderCode = {order.OrderCode} => DONE");
                                                VideoLogger.Info($"================================================================================================================================\n");
                                                if (_isDeleteRFileAfterUploaded)
                                                {
                                                    if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                                                    {
                                                        // Clean up
                                                        File.Delete(videoPath);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                //Lỗi call api up file
                                                isError = true;
                                                VideoLogger.Error("Lỗi call api up file");
                                            }
                                        }
                                        else
                                        {
                                            //download video loi
                                            isError = true;
                                        }
                                    }
                                    else
                                    {
                                        VideoLogger.Error("ProcessDataInQueueAsync => Camera=NULL");
                                        VideoLogger.Error("Không có thông tin camera");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                isError = true;
                                VideoLogger.Error($"{ex.Message}");
                                Thread.Sleep(_delayErrorJobTime * 1000);
                            }
                        }
                        else
                        {
                            isError = true;
                            VideoLogger.Error("ProcessDataInQueueAsync => ORDER=NULL");
                        }

                        if (isError)
                        {
                            //Nếu có lỗi thì add lại vào queue, tí xử lý lại đơn đó
                            _queueMain.Enqueue(session);
                        }
                        isProcessingVideo = false;
                    }
                }
                catch (Exception ex)
                {
                    VideoLogger.Error("ProcessDataInQueueAsync exception " + ex.Message);
                }
                finally
                {
                    Thread.Sleep(_delayJobTime * 1000);
                }
            }
        }

        private string GetPathForVideoUpload(DateTime startTime, DateTime endTime)
        {
            string originalVideoPath = "";
            int retryMax = AppConfig.GetIntValue("ErrorDownloadVideoRetry");
            int retry = 1;
            try
            {
                while (retry <= retryMax)
                {
                    Thread.Sleep(retry * 1000);
                    if (retry > 1)
                    {
                        VideoLogger.Warn($"Redownloading video, retry = {retry}");
                    }
                    originalVideoPath = DownloadOriginalVideo(startTime, endTime);
                    if (!string.IsNullOrEmpty(originalVideoPath) && File.Exists(originalVideoPath))
                    {
                        long sizeFile = new FileInfo(originalVideoPath).Length;
                        if (sizeFile > 0)
                        {
                            return GetResizedVideoPath(originalVideoPath);
                        }
                    }
                    else
                    {
                        VideoLogger.Error("DownloadVideo error");
                        m_lDownHandle = -1;
                    }
                    retry++;
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("GetPathForVideoUpload error");
                MainLogger.Error(ex);
            }
            finally
            {
                if (_isDeleteOFileAfterUploaded)
                {
                    if (!string.IsNullOrEmpty(originalVideoPath) && File.Exists(originalVideoPath))
                    {
                        // Clean up
                        File.Delete(originalVideoPath);
                    }
                }
            }
            return null;
        }

        private async Task<bool> CallApiUploadVideoAsync(Video videoUpload)
        {
            try
            {
                if (videoUpload != null && videoUpload.VideoPaths.Count > 0)
                {
                    APIResult upload = await APIService.UploadVideo(videoUpload);
                    return upload.IsSuccess;
                }
                return false;
            }
            catch (Exception ex)
            {
                MainLogger.Error("CallApiUploadVideo ex");
                MainLogger.Error(ex);
                return false;
            }
        }
    }
}
