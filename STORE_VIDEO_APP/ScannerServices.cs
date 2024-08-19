using Common;
using Common.Model;
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
                string deviceID = GetIDFromPort(portName);
                //var check = _scannerList.FirstOrDefault(x => x.SerialPort.PortName == portName && x.ScannerCode == deviceID);
                //if (check != null)
                //{
                //    MainLogger.Warn($"Connected {portName} does exist before!");
                //    return;
                //}
                if (_serialDevices.Keys.Any(x=>x.Key.Contains(portName)))
                {
                    MainLogger.Warn($"Connected {portName} does exist before!");
                    return;
                }

                
                //Kiểm tra xem thiết bị đang kết nối với cổng COM có phải máy quét không
                if (deviceID.Contains(_serialProductKey))
                {
                    var serialPort = GetSerialPortByPortName(portName);
                    serialPort.DataReceived += DataReceivedHandler;
                    serialPort.Open();

                    if (!_serialDevices.TryAdd(new KeyValuePair<string, SerialPort>(deviceID, serialPort), new StringBuilder()))
                    {
                        MainLogger.Error($"Error connect to scanner {deviceID} port {portName}");
                    }
                    _dataBuffers.TryAdd(portName, new StringBuilder());
                    MainLogger.Info($"Connected to scanner {deviceID} port {portName}.");
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
                try
                {
                    new SerialPort(portName, 9600).Close();
                }
                catch (Exception e)
                {

                    MainLogger.Error($"Close error =>{e}");
                }
            }
        }

        SerialPort GetSerialPortByPortName(string portName)
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
            SerialPort serialPort = (SerialPort)sender;
            string portName = serialPort.PortName;
            try
            {
                StringBuilder dataReader = _dataBuffers[portName];
                if(dataReader != null)
                {
                    //lock (dataReader)
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
        void InitializeScannerEvents()
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

                //WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
                //ManagementEventWatcher watcher = new ManagementEventWatcher(query);
                //watcher.EventArrived += new EventArrivedEventHandler(DeviceChangedEvent);
                //watcher.Start();

            }
            catch (Exception ex)
            {
                MainLogger.Error("ComPortMonitor ex");
                MainLogger.Error(ex);
            }
        }
        void DeviceChangedEvent(object sender, EventArrivedEventArgs e)
        {
            string eventType = e.NewEvent.ClassPath.ClassName;

            // Truy vấn thiết bị để lọc theo loại và PID/VID
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
            foreach (ManagementObject device in searcher.Get())
            {
                string deviceID = device["DeviceID"]?.ToString() ?? "";
                string pnpClass = device["PNPClass"]?.ToString() ?? "";
                string classGuid = device["ClassGuid"]?.ToString() ?? "";

                // Kiểm tra thiết bị có PID/VID đúng và thuộc loại "Port (COM & LPT)"
                if (deviceID.Contains(VID) && deviceID.Contains(PID) && pnpClass == "Ports")
                {
                    Console.WriteLine($"Device of type 'Port (COM & LPT)' with specified PID/VID detected.");
                    Console.WriteLine("Device ID: " + deviceID);

                    InitializeScanner();
                    break; // Thoát khỏi vòng lặp sau khi xử lý thiết bị đầu tiên
                }
            }
        }

        private void DeviceConnectedEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string caption = instance["Caption"]?.ToString();

                if (caption.Contains("COM"))
                {
                    string deviceID = instance["DeviceID"]?.ToString();
                    string port = GetPortName(deviceID);
                    MainLogger.Warn($"Device connected: {caption}, DeviceID: {deviceID} Port {port}");

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

                if (caption.Contains("COM"))
                {
                    string deviceID = instance["DeviceID"]?.ToString();
                    var serialPort = _serialDevices.Keys.FirstOrDefault(x => x.Key == deviceID);
                    if (serialPort.Value != null)
                    {
                        serialPort.Value.Close();
                        _serialDevices.TryRemove(serialPort, out var removedPort);
                    }
                }
            }
            catch (Exception ex)
            {
                MainLogger.Error("DeviceDisconnectedEvent ex");
                MainLogger.Error(ex);
            }
        }

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
    }
}
