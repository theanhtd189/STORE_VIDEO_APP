using Common.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class Function
    {
        public static void WriteLog(string content)
        {
            try
            {
                string filePath = $@"D:\StoreVideoLog_{DateTime.Now.ToString("ddMMyyyy")}.txt";

                // Tạo file nếu chưa tồn tại và ghi nội dung vào file
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine(content);
                }
            }
            catch (Exception)
            {
                //
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
}
