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

        public static bool IsValidJson(string jsonString)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<object>(jsonString);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
            catch (Exception) // if some other exception occurs
            {
                return false;
            }
        }
    }
}
