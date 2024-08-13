using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace CameraServices
{
    public class CameraService
    {
        public string Code { get { return _code; } set { _code = value; } }
        public string CameraIP { set { _ip = value; } }
        public short CameraChannel { set { _channel = value; } }
        public short CameraPort { set { _port = value; } }

        public CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo;
        public CHCNetSDK.NET_DVR_IPPARACFG_V40 m_struIpParaCfgV40;
        public CHCNetSDK.NET_DVR_GET_STREAM_UNION m_unionGetStream;
        public CHCNetSDK.NET_DVR_IPCHANINFO m_struChanInfo;
        public List<string> VideoPaths;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 96, ArraySubType = UnmanagedType.U4)]
        private Int32 _lUserID = -1;
        private Int32 _lDownHandle = -1;
        private Int32 _index = 0;
        private Int32 _lTree = 0;
        private Int16 _port;
        private Int16 _channel;

        private uint dwAChanTotalNum = 0;
        private uint dwDChanTotalNum = 0;
        private uint iLastErr = 0;
        private int[] iChannelNum;
        private int DownloadProgress;

        private bool _bInitSDK = false;
        private bool _isLoginSuccess = false;
        private string _str;
        private string _ip;
        private string _code = "CAMERA_DEFAULT";
        private string _str1;
        private string _str2;
        private int _delayError = AppConfig.GetIntValue("CameraDelayError");
        private bool _isDeleteFileAfterUploaded = AppConfig.GetBooleanValue("IsDeleteFileAfterUploaded");
        private readonly string _userName = AppConfig.GetStringValue("CameraAdminUsername");
        private readonly string _passWord = AppConfig.GetStringValue("CameraAdminPassword");
        private readonly string _videoOutputExtension = AppConfig.GetStringValue("VideoOutputExtension");
        private readonly string _videoResizeOutputExtension = AppConfig.GetStringValue("VideoResizeOutputExtension");

        public CameraService()
        {
            VideoPaths = new List<string>();
            InitConfig();
        }
        private void InitConfig()
        {
            _bInitSDK = CHCNetSDK.NET_DVR_Init();
            if (_bInitSDK == false)
            {
                MainLogger.Info("NET_DVR_Init error!");
                return;
            }
            else
            {
                //Save log of SDK
                CHCNetSDK.NET_DVR_SetLogToFile(3, "C:\\SdkLog\\", true);
                iChannelNum = new int[96];
            }
        }
        private bool Login()
        {
            if (_lUserID < 0)
            {
                string DVRIPAddress = _ip; //IP or domain of device
                Int16 DVRPortNumber = _port;//Service port of device
                string DVRUserName = _userName;//Login name of deivce
                string DVRPassword = _passWord;//Login password of device

                //Login the device
                {
                    try
                    {
                        _lUserID = CHCNetSDK.NET_DVR_Login_V30(DVRIPAddress, DVRPortNumber, DVRUserName, DVRPassword, ref DeviceInfo);
                        if (_lUserID < 0)
                        {
                            iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                            _str1 = "NET_DVR_Login_V30 failed, error code= " + iLastErr; //Login failed,print error code
                            MainLogger.Error(_str1);
                            MainLogger.Error($"CameraCode={_code} | {_ip}:{_port} channel {_channel} ==> Login Failed!");
                            return false;
                        }
                        else
                        {
                            MainLogger.Info($"CameraCode={_code} | {_ip}:{_port} channel {_channel} ==> Login Succesfully!");
                            dwAChanTotalNum = (uint)DeviceInfo.byChanNum;
                            dwDChanTotalNum = (uint)DeviceInfo.byIPChanNum + 256 * (uint)DeviceInfo.byHighDChanNum;

                            if (dwDChanTotalNum > 0)
                            {
                                InfoIPChannel();
                            }
                            else
                            {
                                for (_index = 0; _index < dwAChanTotalNum; _index++)
                                {
                                    ListAnalogChannel(_index + 1, 1);
                                    iChannelNum[_index] = _index + (int)DeviceInfo.byStartChan;
                                }
                                // Logger.InfoMainService("This device has no IP channel!");
                            }
                            return true;
                        }

                    }
                    catch (Exception ex)
                    {
                        MainLogger.Error("Login ex " + ex);
                    }
                }
            }
            return true;
        }
        public void InfoIPChannel()
        {
            uint dwSize = (uint)Marshal.SizeOf(m_struIpParaCfgV40);

            IntPtr ptrIpParaCfgV40 = Marshal.AllocHGlobal((Int32)dwSize);
            Marshal.StructureToPtr(m_struIpParaCfgV40, ptrIpParaCfgV40, false);

            uint dwReturn = 0;
            int iGroupNo = 0; //The demo just acquire 64 channels of first group.If ip channels of device is more than 64,you should call NET_DVR_GET_IPPARACFG_V40 times to acquire more according to group 0~i
            if (!CHCNetSDK.NET_DVR_GetDVRConfig(_lUserID, CHCNetSDK.NET_DVR_GET_IPPARACFG_V40, iGroupNo, ptrIpParaCfgV40, dwSize, ref dwReturn))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                _str1 = "NET_DVR_GET_IPPARACFG_V40 failed, error code= " + iLastErr; //Get IP parameter of configuration failed,print error code.
                MainLogger.Info(_str1);
            }
            else
            {
                // succ
                m_struIpParaCfgV40 = (CHCNetSDK.NET_DVR_IPPARACFG_V40)Marshal.PtrToStructure(ptrIpParaCfgV40, typeof(CHCNetSDK.NET_DVR_IPPARACFG_V40));

                for (_index = 0; _index < dwAChanTotalNum; _index++)
                {
                    ListAnalogChannel(_index + 1, m_struIpParaCfgV40.byAnalogChanEnable[_index]);
                    iChannelNum[_index] = _index + (int)DeviceInfo.byStartChan;
                }

                byte byStreamType;
                uint iDChanNum = 64;

                if (dwDChanTotalNum < 64)
                {
                    iDChanNum = dwDChanTotalNum; //If the ip channels of device is less than 64,will get the real channel of device
                }

                for (_index = 0; _index < iDChanNum; _index++)
                {
                    iChannelNum[_index + dwAChanTotalNum] = _index + (int)m_struIpParaCfgV40.dwStartDChan;

                    byStreamType = m_struIpParaCfgV40.struStreamMode[_index].byGetStreamType;
                    m_unionGetStream = m_struIpParaCfgV40.struStreamMode[_index].uGetStream;

                    switch (byStreamType)
                    {
                        //At present NVR just support case 0-one way to get stream from device
                        case 0:
                            dwSize = (uint)Marshal.SizeOf(m_unionGetStream);
                            IntPtr ptrChanInfo = Marshal.AllocHGlobal((Int32)dwSize);
                            Marshal.StructureToPtr(m_unionGetStream, ptrChanInfo, false);
                            m_struChanInfo = (CHCNetSDK.NET_DVR_IPCHANINFO)Marshal.PtrToStructure(ptrChanInfo, typeof(CHCNetSDK.NET_DVR_IPCHANINFO));

                            //List ip channels
                            ListIPChannel(_index + 1, m_struChanInfo.byEnable, m_struChanInfo.byIPID);
                            Marshal.FreeHGlobal(ptrChanInfo);
                            break;

                        default:
                            break;
                    }
                }
            }
            Marshal.FreeHGlobal(ptrIpParaCfgV40);
        }
        public void ListIPChannel(Int32 iChanNo, byte byOnline, byte byIPID)
        {
            _str1 = String.Format("IPCamera {0}", iChanNo);
            _lTree++;

            if (byIPID == 0)
            {
                _str2 = "X"; //The ip channel is empty,no front-end(such as camera)is added               
            }
            else
            {
                if (byOnline == 0)
                {
                    _str2 = "offline"; //The channel is offline
                }
                else
                    _str2 = "online"; //The channel is online
            }

            //listViewIPChannel.Items.Add(new ListViewItem(new string[] { str1, str2 }));//Add channels to list
        }
        public void ListAnalogChannel(Int32 iChanNo, byte byEnable)
        {
            _str1 = String.Format("Camera {0}", iChanNo);
            _lTree++;

            if (byEnable == 0)
            {
                _str2 = "Disabled"; //This channel has been disabled               
            }
            else
            {
                _str2 = "Enabled"; //This channel has been enabled  
            }
        }
        public static void ExecuteFFmpegCommand(string arguments, out string duration)
        {
            duration = string.Empty;
            try
            {
                Process ffmpeg = new Process();
                ffmpeg.StartInfo.FileName = "ffmpeg";
                ffmpeg.StartInfo.Arguments = $"{arguments}";
                ffmpeg.StartInfo.UseShellExecute = false;
                //ffmpeg.StartInfo.RedirectStandardOutput = true;
                ffmpeg.StartInfo.RedirectStandardError = true;
                //ffmpeg.StartInfo.CreateNoWindow = false;
                ffmpeg.Start();
                //string output = ffmpeg.StandardOutput.ReadToEnd();
                string error = ffmpeg.StandardError.ReadToEnd();
                ffmpeg.WaitForExit();
                duration = ParseDuration(error);
            }
            catch (Exception ex)
            {
                MainLogger.Info(ex);
            }
        }

        /// <summary>
        /// Get video đã được giảm chất lượng
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public List<string> GetResizedVideoPath(DateTime startTime, DateTime endTime)
        {
            string originalVideoPath = "";
            try
            {
                long sizeFile = 0;

                VideoPaths.Clear();

                while (sizeFile == 0)
                {
                    originalVideoPath = GetOriginalVideoPath(startTime, endTime, out bool isVideoCorrupted);

                    if (!string.IsNullOrEmpty(originalVideoPath) && File.Exists(originalVideoPath) && !isVideoCorrupted)
                    {
                        sizeFile = new FileInfo(originalVideoPath).Length;
                        if (sizeFile > 0)
                        {
                            string outputFilePath = originalVideoPath.Replace(_videoOutputExtension, _videoResizeOutputExtension);
                            string cmd = string.Format("-i \"{0}\" -vcodec libx265 -crf 28 \"{1}\"", originalVideoPath, outputFilePath);
                            try
                            {
                                MainLogger.Info($"Start resizing file ...");
                                ExecuteFFmpegCommand(cmd, out string duration);
                                if (File.Exists(outputFilePath) && new FileInfo(outputFilePath).Length > 0)
                                {
                                    MainLogger.Info($"Resized file {outputFilePath} successfully");
                                    VideoPaths.Add(outputFilePath);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                MainLogger.Error(ex);
                                Thread.Sleep(5000);
                            }
                        }
                        else
                        {
                            MainLogger.Error($"File {originalVideoPath} render failed!");
                            Thread.Sleep(5000);
                        }
                    }
                    //Thread.Sleep(5000);
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error(ex);
            }
            finally
            {
                if (_isDeleteFileAfterUploaded && !string.IsNullOrEmpty(originalVideoPath) && File.Exists(originalVideoPath))
                {
                    File.Delete(originalVideoPath);
                }
            }
            return VideoPaths;
        }

        /// <summary>
        /// Get video chất lượng gốc 
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        private string GetOriginalVideoPath(DateTime startTime, DateTime endTime, out bool isVideoCorrupted)
        {
            isVideoCorrupted = false;
            try
            {
                InitConfig();

                while (!_isLoginSuccess)
                {
                    _isLoginSuccess = Login();
                }
                if (_lDownHandle >= 0)
                {
                    MainLogger.Info("Downloading, please stop firstly!");//Please stop downloading
                    return null;
                }

                CHCNetSDK.NET_DVR_PLAYCOND struDownPara = new CHCNetSDK.NET_DVR_PLAYCOND();
                struDownPara.dwChannel = (uint)_channel; //Channel number  

                //Set the starting time
                struDownPara.struStartTime.dwYear = (uint)startTime.Year;
                struDownPara.struStartTime.dwMonth = (uint)startTime.Month;
                struDownPara.struStartTime.dwDay = (uint)startTime.Day;
                struDownPara.struStartTime.dwHour = (uint)startTime.Hour;
                struDownPara.struStartTime.dwMinute = (uint)startTime.Minute;
                struDownPara.struStartTime.dwSecond = (uint)startTime.Second;

                //Set the stopping time
                struDownPara.struStopTime.dwYear = (uint)endTime.Year;
                struDownPara.struStopTime.dwMonth = (uint)endTime.Month;
                struDownPara.struStopTime.dwDay = (uint)endTime.Day;
                struDownPara.struStopTime.dwHour = (uint)endTime.Hour;
                struDownPara.struStopTime.dwMinute = (uint)endTime.Minute;
                struDownPara.struStopTime.dwSecond = (uint)endTime.Second;

                string videoFileName = $"{startTime:ddMMyyyy-HHmmss}-{endTime:HHmmss-}{struDownPara.dwChannel}{_videoOutputExtension}";
                string videoFolderPath = Path.Combine(AppConfig.GetStringValue("StoreVideoPath"), Code);
                string fullFilePath = Path.Combine(videoFolderPath, videoFileName);

                if (!Directory.Exists(videoFolderPath))
                {
                    Directory.CreateDirectory(videoFolderPath);
                    MainLogger.Info($"Created path: {videoFolderPath}");
                }
                string sVideoFileName = "D:\\Downtest_Channel" + struDownPara.dwChannel + ".mp4";
                //Download by time
                _lDownHandle = CHCNetSDK.NET_DVR_GetFileByTime_V40(_lUserID, fullFilePath, ref struDownPara);
                if (_lDownHandle < 0)
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    _str = "NET_DVR_GetFileByTime_V40 failed, error code= " + iLastErr;
                    MainLogger.Error(_str);
                    Debug.WriteLine(_str);
                    return null;
                }

                uint iOutValue = 0;
                if (!CHCNetSDK.NET_DVR_PlayBackControl_V40(_lDownHandle, CHCNetSDK.NET_DVR_PLAYSTART, IntPtr.Zero, 0, IntPtr.Zero, ref iOutValue))
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    _str = "NET_DVR_PLAYSTART failed, error code= " + iLastErr; //Download controlling failed,print error code
                    MainLogger.Error(_str);
                    return null;
                }

                if (CheckStreamStatus())
                {
                    if (File.Exists(fullFilePath) && new FileInfo(fullFilePath).Length > 0)
                    {
                        TimeSpan expectedDuration = TimeSpan.Parse((endTime - startTime).ToString(@"hh\:mm\:ss"));//Thời lượng video dự kiến nếu render thành công
                        ExecuteFFmpegCommand($"-i \"{fullFilePath}\"", out string duration);
                        MainLogger.Info($"Created file => {fullFilePath}");
                        if (TimeSpan.TryParse(duration, out TimeSpan actualDuration))
                        {
                            //Tính xem thời lượng 2 video chênh lệch nhiều không 
                            TimeSpan difference = expectedDuration - actualDuration;

                            //Tính xem 50% thời lượng của video gốc là bao nhiêu
                            TimeSpan fiftyPercentOfA = TimeSpan.FromTicks(expectedDuration.Ticks / 2);

                            //Nếu thời lượng chênh nhau quá 50% thì video render ra đang bị corrupted
                            if (difference > fiftyPercentOfA)
                            {
                                isVideoCorrupted = true;
                                MainLogger.Error($"!!! WARNING: Video was corrupted");
                                MainLogger.Error($"Expected duration: {expectedDuration} | Actual duration: {duration}");
                            }
                        }
                        else
                        {
                            MainLogger.Info($"Video duration: {duration}");
                        }
                        return fullFilePath;
                    }
                    else
                    {
                        MainLogger.Error($"Render failed. Retry...");
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("GetOriginalVideoPath ex");
                MainLogger.Error(ex);
            }
            return null;
        }
        private bool CheckStreamStatus()
        {
            while (true)
            {
                int iPos = CHCNetSDK.NET_DVR_GetDownloadPos(_lDownHandle);
                if (iPos > 0 && iPos < 100)
                {
                    DownloadProgress = iPos;
                    //Logger.InfoMainService($"Download Progress: {DownloadProgress}%");
                }

                if (iPos == 100) // Finish downloading
                {
                    DownloadProgress = iPos;
                    //Logger.InfoMainService($"Download Progress: {DownloadProgress}%");
                    if (!CHCNetSDK.NET_DVR_StopGetFile(_lDownHandle))
                    {
                        iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                        _str = "NET_DVR_StopGetFile failed, error code= " + iLastErr; // Download controlling failed, print error code
                        MainLogger.Error(_str);
                        return false;
                    }
                    _lDownHandle = -1;
                    return true;
                }

                if (iPos == 200) // Network abnormal, download failed
                {
                    MainLogger.Error("The downloading is abnormal due to the abnormal network!");
                    return false;
                }
            }
        }
        static string ParseDuration(string ffmpegOutput)
        {
            try
            {
                var durationLine = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"Duration: (\d{2}:\d{2}:\d{2}\.\d{2})");
                return durationLine.Success ? durationLine.Groups[1].Value : "Unknown";
            }
            catch (Exception ex)
            {
                MainLogger.Error(ex);
            }
            return string.Empty;
        }

        //private void btn_Exit_Click(object sender, EventArgs e)
        //{
        //    //Stop download
        //    if (m_lDownHandle >= 0)
        //    {
        //        CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle);
        //        m_lDownHandle = -1;
        //    }

        //    //Logout the device
        //    if (m_lUserID >= 0)
        //    {
        //        CHCNetSDK.NET_DVR_Logout(m_lUserID);
        //        m_lUserID = -1;
        //    }

        //    //Application.Exit();
        //}
    }

}
