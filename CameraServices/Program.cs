using CameraServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace CameraServices
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Test();
        }
        static void Test()
        {
            DateTime a = DateTime.Now.AddMinutes(-1).AddSeconds(-20);
            DateTime b = DateTime.Now.AddMinutes(-1).AddSeconds(-10);
            string ip = "192.168.1.168";
            var cam = new CameraService()
            {
                CameraPort = 8000,
                CameraIP = ip,
                CameraChannel = 8
            };
            var result = cam.GetResizedVideoPath(a, b);


            Console.ReadLine();
        }
    }
}
