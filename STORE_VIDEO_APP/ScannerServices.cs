using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;

namespace STORE_VIDEO_APP
{
    public partial class AppMainService
    {
        private ConcurrentDictionary<string, StringBuilder> _dataBuffers;
        private ConcurrentDictionary<string, SerialPort> _serialDevices;
        private readonly string _pid = AppConfig.GetStringValue("ProductID");
        private readonly string _vid = AppConfig.GetStringValue("VendorID");
        private ManagementEventWatcher _watcher;
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
                ComPortMonitor();
                _serialDevices = new ConcurrentDictionary<string, SerialPort>();
                _dataBuffers = new ConcurrentDictionary<string, StringBuilder>();
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
                MainLogger.Error("InitializeScannerService ex");
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

                    MainLogger.Info($"\n------------------------------");
                    MainLogger.Info($"ID máy quét: {idMachine}");
                    MainLogger.Info($"Dữ liệu đọc được: {line}");

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
        void ComPortMonitor()
        {
            try
            {
                // Set up the WMI query for detecting new serial ports
                string query = "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_SerialPort'";
                WqlEventQuery creationQuery = new WqlEventQuery(query);

                _watcher = new ManagementEventWatcher(creationQuery);
                _watcher.EventArrived += new EventArrivedEventHandler(OnPortAdded);
                _watcher.Start();
            }
            catch (Exception ex)
            {
                MainLogger.Error("ComPortMonitor ex");
                MainLogger.Error(ex);
            }
        }

        private void OnPortAdded(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string portName = targetInstance["DeviceID"].ToString();
                Console.WriteLine($"Serial port added: {portName}");
            }
            catch (Exception ex)
            {
                MainLogger.Error("OnPortAdded ex");
                MainLogger.Error(ex);
            }
        }

        public void Stop()
        {
            _watcher.Stop();
        }
    }
}
