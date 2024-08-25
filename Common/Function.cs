using Common.Model;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Common
{
    public static class Function
    {
        static readonly string logfilePath = $@"C:\StoreVideo\Logs\{DateTime.Now:yyyyMMdd}\AppInitLog.txt";
        public static void WriteLog(string content)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(logfilePath);

                // Check if the directory exists
                if (!Directory.Exists(directoryPath))
                {
                    // Create the directory if it doesn't exist
                    Directory.CreateDirectory(directoryPath);
                }

                using (StreamWriter writer = new StreamWriter(logfilePath, true))
                {
                    writer.WriteLine(content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WriteLog ex {ex.Message}");
                Debug.WriteLine($"WriteLog ex {ex.Message}");
            }
        }

        public static bool IsValidQRJson(string jsonString)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<QRData>(jsonString);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static string ToJson(this object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
    public static class ServerTimeHelper
    {
        private static ServerTime _serverTime = null;
        public static DateTime GetUnixTimeSeconds()
        {
            try
            {
                if (_serverTime == null)
                {
                    _serverTime = new ServerTime();
                }
                return _serverTime.GetCurrentServerTime();
            }
            catch
            {

                return DateTime.Now;
            }

        }
    }
    public class ServerTime
    {
        private DateTime startTime;
        private TimeSpan offset;

        public ServerTime()
        {
            // Store the current GMT+0000 time at startup
            startTime = DateTime.Now;

            // Assuming server time is also the current UTC time
            // If the server time is different, adjust this accordingly
            var serverTime = DateTime.Now;
#pragma warning disable CS0168 // Variable is declared but never used
            try
            {
                const int tryTimes = 5;
                int currentTryTimes = 0;
                while (currentTryTimes < tryTimes)
                {
#pragma warning disable CS0168 // Variable is declared but never used
                    try
                    {
                        serverTime = GetNetworkTime();
                        break;
                    }
                    catch (Exception ex)
                    {
                        //MessageBox.Show(ex.ToString());
                        currentTryTimes++;
                    }
#pragma warning restore CS0168 // Variable is declared but never used
                }

            }
            catch (Exception ex)
            {

            }
#pragma warning restore CS0168 // Variable is declared but never used

            // Calculate the time difference
            offset = serverTime - startTime;
        }

        public DateTime GetCurrentServerTime()
        {
            // Get the current time and apply the time difference
            return DateTime.Now + offset;
        }

        public long ToUnixTimeSeconds()
        {
            // Convert the current server time to Unix Time
            return ((DateTimeOffset)GetCurrentServerTime()).ToUnixTimeSeconds();
        }
        private static DateTime GetNetworkTime()
        {
            //default Windows time server
            const string ntpServer = "time.google.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 1000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }

        // stackoverflow.com/a/3294698/162671
        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}
