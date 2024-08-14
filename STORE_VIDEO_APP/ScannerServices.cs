using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;

namespace STORE_VIDEO_APP
{
    public partial class AppMainService
    {
        private ConcurrentDictionary<string, StringBuilder> _dataBuffers = new ConcurrentDictionary<string, StringBuilder>();
        private ConcurrentDictionary<string, SerialPort> _serialDevices = new ConcurrentDictionary<string, SerialPort>();
        private readonly string _pid = AppConfig.GetStringValue("ProductID");
        private readonly string _vid = AppConfig.GetStringValue("VendorID");
        private static HashSet<string> _previousDeviceIds = new HashSet<string>();
        private List<string> ListCOMPorts
        {
            get
            {
                try
                {
                    return SerialPort.GetPortNames().ToList();
                }
                catch (Exception ex)
                {
                    MainLogger.Error("get listCOMPorts " + ex);
                    return new List<string>();
                }
            }
        }
        private void InitializeScannerService()
        {
            try
            {
                InitializeScanner();
                InitializeScannerEvents();
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializeScannerService ex");
                MainLogger.Error(ex);
            }
        }

        private void InitializeScanner()
        {
            try
            {
                foreach (var port in ListCOMPorts)
                {
                    InitializeSerialPort(port, 9600);
                }
                if (_serialDevices.Count == 0)
                {
                    MainLogger.Warn("Chưa có máy quét nào được kết nối");
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializeScanner ex");
                MainLogger.Error(ex);
            }
        }

        private void InitializeSerialPort(string portName, int baudRate)
        {
            try
            {
                string deviceID = GetIDFromPort(portName);
                string serialProductKey = $"VID_{_vid}&PID_{_pid}";

                //Kiểm tra xem thiết bị đang kết nối với cổng COM có phải máy quét không
                if (deviceID.Contains(serialProductKey))
                {
                    var serialPort = new SerialPort(portName, baudRate)
                    {
                        Encoding = Encoding.ASCII
                    };
                    serialPort.DataReceived += DataReceivedHandler;
                    serialPort.Open();

                    if (!_serialDevices.TryAdd(portName, serialPort))
                    {
                        MainLogger.Info($"Lỗi kết nối máy quét {deviceID} cổng {portName}.");
                    }
                    _dataBuffers.TryAdd(portName, new StringBuilder());
                    MainLogger.Info($"Đã kết nối máy quét {deviceID} cổng {portName}.");
                }
                else
                {
                    //CommonFunction.WriteLog($"Day khong phai may quet!");
                }

            }
            catch (Exception ex)
            {
                MainLogger.Error($"InitializeSerialPort {portName}.");
                MainLogger.Error(ex);
            }
        }
        private void ProcessBuffer(string portName)
        {
            try
            {
                var dataBuffer = _dataBuffers[portName];
                var dataString = dataBuffer.ToString();

                int newLineIndex;
                while ((newLineIndex = dataString.IndexOf('\r')) >= 0)
                {
                    var line = dataString.Substring(0, newLineIndex);
                    dataString = dataString.Substring(newLineIndex + 1); // Bỏ qua ký tự '\r'
                    string idMachine = GetIDFromPort(portName);
                    if (!string.IsNullOrEmpty(idMachine))
                    {
                        ProcessCode(idMachine, line);
                    }
                    else
                    {
                        MainLogger.Warn($"Không đọc được thông tin máy quét");
                    }
                }

                // Xóa buffer và thêm lại bất kỳ dòng không hoàn chỉnh nào
                dataBuffer.Clear();
                dataBuffer.Append(dataString);
            }
            catch (Exception ex)
            {
                MainLogger.Error("ProcessBuffer ex");
                MainLogger.Error(ex);
            }
        }

        /// <summary>
        /// Get Device ID by its COM port name
        /// </summary>
        /// <param name="portName"></param>
        /// <returns></returns>
        private string GetIDFromPort(string portName)
        {
            string id = "";
            try
            {
                string query = "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(" + portName + ")%'";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                ManagementObjectCollection results = searcher.Get();

                if (results.Count > 0)
                {
                    // Lấy phần tử đầu tiên trong kết quả truy vấn
                    ManagementObject obj = results.OfType<ManagementObject>().FirstOrDefault();
                    if (obj != null)
                    {
                        return obj["DeviceID"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("GetIDFromPort ex");
                MainLogger.Error(ex);
            }
            return id;
        }
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            var serialPort = (SerialPort)sender;
            var portName = serialPort.PortName;
            try
            {
                lock (_dataBuffers[portName])
                {
                    var buffer = new byte[serialPort.BytesToRead];
                    serialPort.Read(buffer, 0, buffer.Length);
                    var dataInput = Encoding.ASCII.GetString(buffer);
                    _dataBuffers[portName].Append(dataInput);

                    // Xử lý dữ liệu trong buffer
                    ProcessBuffer(portName);
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"DataReceivedHandler ex");
                MainLogger.Error(ex);
            }
        }
        void InitializeScannerEvents()
        {
            try
            {
                // Lắng nghe sự kiện khi có thiết bị kết nối
                ManagementEventWatcher connectWatcher = new ManagementEventWatcher();
                connectWatcher.EventArrived += new EventArrivedEventHandler(DeviceConnected);
                connectWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
                connectWatcher.Start();

                // Lắng nghe sự kiện khi thiết bị bị ngắt kết nối
                ManagementEventWatcher disconnectWatcher = new ManagementEventWatcher();
                disconnectWatcher.EventArrived += new EventArrivedEventHandler(DeviceDisconnected);
                disconnectWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");
                disconnectWatcher.Start();

            }
            catch (Exception ex)
            {
                MainLogger.Error("ComPortMonitor ex");
                MainLogger.Error(ex);
            }
        }

        private void DeviceConnected(object sender, EventArrivedEventArgs e)
        {
            MainLogger.Info("A device has been connected.");
            InitializeScanner();
        }

        private void DeviceDisconnected(object sender, EventArrivedEventArgs e)
        {
            MainLogger.Info("A device has been disconnected.");
            //InitializeScanner();
        }
    }
}
