﻿using Serilog;
using System;
using System.Diagnostics;

namespace Common
{
    public static class MainLogger
    {
        private static ILogger _instance;
        public static ILogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    string logFolderName = AppConfig.GetStringValue("LogFolderName") ?? "Logs";
                    if (logFolderName.EndsWith("/"))
                    {
                        logFolderName = logFolderName.TrimEnd('/');
                    }
                    string logExtension = AppConfig.GetStringValue("LogExtension") ?? "txt";
                    string time = DateTime.Now.ToString("yyyyMMdd");
                    _instance = new LoggerConfiguration()
                                    .MinimumLevel.Debug()
                                    .WriteTo.File(
                                        path: $"{logFolderName}/{time}/MainServiceLog.{logExtension}",
                                        shared: true)
                                    .WriteTo.Console()
                                    .CreateLogger();
                }
                return _instance;
            }
        }

        public static void Info(object obj)
        {
            Debug.WriteLine(obj);
            Instance.Information(obj.ToString());
        }

        public static void Error(object obj)
        {
            Debug.WriteLine(obj);
            Instance.Error(obj.ToString());
            ErrorLogger.Error("[MAIN SERVICE]: " + obj);
        }

        public static void Warn(object obj)
        {
            Debug.WriteLine(obj);
            Instance.Warning(obj.ToString());
        }
    }
    public static class VideoLogger
    {
        private static ILogger _instance;
        public static ILogger LoggerInstance
        {
            get
            {
                if (_instance == null)
                {
                    string logFolderName = AppConfig.GetStringValue("LogFolderName") ?? "Logs";
                    if (logFolderName.EndsWith("/"))
                    {
                        logFolderName = logFolderName.TrimEnd('/');
                    }
                    string logExtension = AppConfig.GetStringValue("LogExtension") ?? "txt";
                    string time = DateTime.Now.ToString("yyyyMMdd");
                    _instance = new LoggerConfiguration()
                                    .MinimumLevel.Debug()
                                    .WriteTo.File(
                                        path: $"{logFolderName}/{time}/VideoServiceLog.{logExtension}",
                                        shared: true)
                                    .WriteTo.Console()
                                    .CreateLogger();
                }
                return _instance;
            }
        }

        public static void Info(object obj)
        {
            Debug.WriteLine(obj);
            LoggerInstance.Information(obj.ToString());
        }

        public static void Error(object obj)
        {
            Debug.WriteLine(obj);
            LoggerInstance.Error(obj.ToString());
            ErrorLogger.Error("[VIDEO SERVICE]: " + obj);
        }

        public static void Warn(object obj)
        {
            Debug.WriteLine(obj);
            LoggerInstance.Warning(obj.ToString());
        }
    }
    public static class ErrorLogger
    {
        private static ILogger _instance;
        public static ILogger LoggerInstance
        {
            get
            {
                if (_instance == null)
                {
                    string logFolderName = AppConfig.GetStringValue("LogFolderName") ?? "Logs";
                    if (logFolderName.EndsWith("/"))
                    {
                        logFolderName = logFolderName.TrimEnd('/');
                    }
                    string logExtension = AppConfig.GetStringValue("LogExtension") ?? "txt";
                    string time = DateTime.Now.ToString("yyyyMMdd");
                    _instance = new LoggerConfiguration()
                                    .WriteTo.File(
                                        path: $"{logFolderName}/{time}/ErrorLog.{logExtension}",
                                        shared: true)
                                    .CreateLogger();
                }
                return _instance;
            }
        }

        static ErrorLogger()
        {
            LoggerInstance.Error("================================================================================================================================");
        }

        public static void Info(object obj)
        {
            Debug.WriteLine(obj);
            LoggerInstance.Information(obj.ToString());
        }

        public static void Error(object obj)
        {
            Debug.WriteLine(obj);
            LoggerInstance.Error(obj.ToString());
        }

        public static void Warn(object obj)
        {
            Debug.WriteLine(obj);
            LoggerInstance.Warning(obj.ToString());
        }
    }

}
