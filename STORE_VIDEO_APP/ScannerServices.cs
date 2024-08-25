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
    /// <summary>
    /// Scanner Services
    /// </summary>
    public partial class AppMainService
    {
        #region FIELDS, PROPERTIES
        private ConcurrentDictionary<string, StringBuilder> _dataBuffers = new ConcurrentDictionary<string, StringBuilder>();

        private ConcurrentDictionary<KeyValuePair<string, SerialPort>, StringBuilder> _serialDevices = new ConcurrentDictionary<KeyValuePair<string, SerialPort>, StringBuilder>();
        private static string PID => $"PID_{AppConfig.GetStringValue("ProductID")}";
        private static string VID => $"VID_{AppConfig.GetStringValue("VendorID")}";

        static readonly string _serialProductKey = $"{VID}&{PID}";
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
        #endregion

        #region INIT
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
                    InitializeSerialPort(port);
                }
                if (_serialDevices.Count == 0)
                {
                    MainLogger.Warn("Not found any scanners");
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializeScanner ex");
                MainLogger.Error(ex);
            }
        }
        private void InitializeSerialPort(string portName)
        {
            try
            {
                MainLogger.Info($"InitializeSerialPort {portName}");
                if (_serialDevices.Keys.Any(x => x.Key.Contains(portName)))
                {
                    MainLogger.Warn($"Connected {portName} does exist before!");
                    return;
                }

                //Kiểm tra xem thiết bị đang kết nối với cổng COM có phải máy quét không
                string deviceID = GetIDFromPort(portName);
                if (deviceID.Contains(_serialProductKey))
                {
                    var serialPort = GetSerialPortByPortName(portName);
                    serialPort.DataReceived += DataReceivedHandler;
                    serialPort.Open();

                    if (_serialDevices.TryAdd(new KeyValuePair<string, SerialPort>(deviceID, serialPort), new StringBuilder()))
                    {
                        _dataBuffers.TryAdd(portName, new StringBuilder());
                        MainLogger.Info($"Connected to scanner {deviceID} port {portName}.");
                    }
                    else
                    {
                        MainLogger.Error($"Error connect to scanner {deviceID} port {portName}");
                    }
                }
                else
                {
                    //CommonFunction.WriteLog($"Day khong phai may quet!");
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"InitializeSerialPort ex: {ex}");
            }
        }
        private void InitializeScannerEvents()
        {
            try
            {
                // Lắng nghe sự kiện khi thiết bị được kết nối hoặc ngắt kết nối qua COM port
                WqlEventQuery creationQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
                WqlEventQuery deletionQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");

                ManagementEventWatcher creationWatcher = new ManagementEventWatcher(creationQuery);
                ManagementEventWatcher deletionWatcher = new ManagementEventWatcher(deletionQuery);

                creationWatcher.EventArrived += new EventArrivedEventHandler(DeviceConnectedEvent);
                deletionWatcher.EventArrived += new EventArrivedEventHandler(DeviceDisconnectedEvent);

                creationWatcher.Start();
                deletionWatcher.Start();

            }
            catch (Exception ex)
            {
                MainLogger.Error("ComPortMonitor ex");
                MainLogger.Error(ex);
            }
        }
        #endregion

        #region EVENT
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            string portName = serialPort.PortName;

            // Đảm bảo khóa tồn tại cho mỗi cổng
            var portLock = _locks.GetOrAdd(portName, new object());

            try
            {
                lock (portLock) // Khóa cụ thể cho cổng
                {
                    StringBuilder dataReader = _dataBuffers[portName];
                    if (dataReader != null)
                    {
                        var buffer = new byte[serialPort.BytesToRead];
                        serialPort.Read(buffer, 0, buffer.Length);
                        var dataInput = Encoding.ASCII.GetString(buffer);
                        dataReader.Append(dataInput);

                        // Xử lý dữ liệu trong buffer
                        ProcessBuffer(portName);
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error($"DataReceivedHandler ex");
                MainLogger.Error(ex);
            }
        }
        private void DeviceConnectedEvent(object sender, EventArrivedEventArgs e)
        {

            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string caption = instance["Caption"]?.ToString();
                MainLogger.Warn($"DeviceConnectedEvent " + caption);
                if (caption.Contains("COM"))
                {
                    string deviceID = instance["DeviceID"]?.ToString();
                    string port = GetPortName(deviceID);
                    InitializeSerialPort(port);
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("DeviceConnectedEvent ex");
                MainLogger.Error(ex);
            }
        }
        private void DeviceDisconnectedEvent(object sender, EventArrivedEventArgs e)
        {

            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string caption = instance["Caption"]?.ToString();
                MainLogger.Warn($"DeviceDisconnectedEvent " + caption);
                if (caption.Contains("COM"))
                {
                    string deviceID = instance["DeviceID"]?.ToString();
                    var serialPort = _serialDevices.Keys.FirstOrDefault(x => x.Key == deviceID);
                    if (serialPort.Value != null)
                    {
                        serialPort.Value.Close();
                        _serialDevices.TryRemove(serialPort, out var removedSerialPort);
                        _dataBuffers.TryRemove(serialPort.Value.PortName, out var removedDataPort);
                        MainLogger.Warn($"Removed scanner {deviceID} port {serialPort.Value.PortName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("DeviceDisconnectedEvent ex");
                MainLogger.Error(ex);
            }
        }
        #endregion

        #region FUNCTION
        private string GetPortName(string deviceID)
        {
            try
            {
                // Truy vấn Win32_PnPEntity dựa trên DeviceID
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceID.Replace("\\", "\\\\")}'";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        // Kiểm tra nếu tên cổng COM có trong chuỗi Caption
                        string caption = obj["Caption"]?.ToString();
                        if (!string.IsNullOrEmpty(caption) && caption.Contains("(COM"))
                        {
                            // Tách tên cổng COM từ Caption (ví dụ: "USB Serial Port (COM3)")
                            int startIndex = caption.IndexOf("(COM") + 1;
                            int endIndex = caption.IndexOf(")", startIndex);
                            if (startIndex > 0 && endIndex > 0)
                            {
                                return caption.Substring(startIndex, endIndex - startIndex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("GetPortName ex");
                MainLogger.Error(ex);
            }
            return null;
        }
        private SerialPort GetSerialPortByPortName(string portName)
        {
            // Kiểm tra xem cổng COM có tồn tại không
            string[] availablePorts = SerialPort.GetPortNames();

            if (Array.Exists(availablePorts, port => port.Equals(portName, StringComparison.OrdinalIgnoreCase)))
            {
                // Tạo và cấu hình đối tượng SerialPort
                SerialPort serialPort = new SerialPort(portName)
                {
                    BaudRate = 9600, // Cấu hình tốc độ Baud theo nhu cầu của bạn
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                return serialPort;
            }

            return new SerialPort(portName, 9600)
            {
                Encoding = Encoding.ASCII
            }; ;
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
        #endregion
    }
}
