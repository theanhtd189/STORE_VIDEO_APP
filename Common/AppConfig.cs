using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Common
{
    public static class AppConfig
    {
        private static NameValueCollection _config;
        public static NameValueCollection GlobalConfig
        {
            get
            {
                if (_config == null)
                {
                    _config = GetAppConfig();
                }
                return _config;
            }
        }
        public static bool IsTestEnviroment => AppConfig.GetBooleanValue("EnableTest");
        public static string APIHostName =>  AppConfig.GetStringValue("APIHostName");
        public static string CameraHostName =>  AppConfig.GetStringValue("CameraAdminIP");

        private static NameValueCollection GetAppConfig()
        {
            NameValueCollection appSettings = new NameValueCollection();
            try
            {
                Function.WriteLog("GetAppConfig ");
                string appConfigName = "AppConfig.xml";
                string currentDirectory = Directory.GetCurrentDirectory();
                Function.WriteLog("currentDirectory "+ currentDirectory);
                // Đường dẫn đến file AppConfig.xml
                string configFilePath = Path.Combine(currentDirectory, appConfigName);
                Function.WriteLog("configFilePath " + configFilePath);
                if (!File.Exists(configFilePath))
                {
                    Function.WriteLog("Not found AppConfig.xml");
                    return appSettings;
                }
                else
                {
                    Function.WriteLog("Found AppConfig.xml");
                }

                // Tạo XmlDocument để đọc file cấu hình
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(configFilePath);

                // Nạp cấu hình vào NameValueCollection

                foreach (XmlNode node in xmlDocument.SelectNodes("configuration/appSettings/add"))
                {
                    if (node.Attributes["key"] != null && node.Attributes["value"] != null)
                    {
                        appSettings.Add(node.Attributes["key"].Value, node.Attributes["value"].Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Function.WriteLog("GetAppConfig ex");
                Function.WriteLog(ex.ToString());
            }
            return appSettings;
        }
        public static string GetStringValue(string name)
        {
            try
            {
                if (!string.IsNullOrEmpty(name))
                {
                    return GlobalConfig[name];
                }
            }
            catch (Exception ex)
            {
                Function.WriteLog("GetStringValue ex");
                Function.WriteLog(ex.ToString());
            }
            return null;
        }
        public static int GetIntValue(string name)
        {
            try
            {
                if (!string.IsNullOrEmpty(name))
                {
                    string value = GlobalConfig[name];
                    if (value != null)
                    {
                        bool check = int.TryParse(value, out int outResult);
                        return check ? outResult : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Function.WriteLog("GetIntValue ex");
                Function.WriteLog(ex.ToString());
            }
            return 0;
        }
        public static bool GetBooleanValue(string name)
        {
            try
            {
                if (!string.IsNullOrEmpty(name))
                {
                    int value = GetIntValue(name);
                    return value == 1 ? true : false;
                }
            }
            catch (Exception ex)
            {
                Function.WriteLog("GetStringValue ex");
                Function.WriteLog(ex.ToString());
            }
            return false;
        }


    }
}
