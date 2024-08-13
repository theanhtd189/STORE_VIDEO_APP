using ApiServices;
using CameraServices;
using Common;
using Common.Model;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Xml;
using App = Common.Model;

namespace Test
{
    class Test_Program
    {
        static DateTime startTime = DateTime.Now.AddMinutes(-2);
        static DateTime endTime = DateTime.Now.AddMinutes(-1);
        static string format = "dd/MM/yyyy HH:mm:ss";
        static string ip = "171.241.25.75";
        static CameraServices.CameraService _camera;
        static string json = "{\"UserId\":\"a7cd6c60-cea7-4aae-a750-514e1d74d1e8\",\"DeskId\":4,\"Command\":\"STARTSESSION\",\"Cameras\":[{\"Code\":\"CAM001\",\"Name\":\"CAM001\",\"Image\":null,\"Note\":null,\"DeskId\":null,\"DeskCode\":null,\"CameraIP\":\"171.241.25.75\",\"CameraChannel\":\"36\",\"CameraPort\":8000,\"created_by\":null,\"created_date\":\"0001-01-01T00:00:00\",\"modified_by\":null,\"modified_date\":\"0001-01-01T00:00:00\",\"IsDelete\":false,\"Id\":6}]}";


        static string uid = @"a7cd6c60-cea7-4aae-a750-514e1d74d1e8";

        public static App.Order CurrentOrder { get; private set; }

        static void Main(string[] args)
        {
            Testcamera();
            Console.ReadLine();
        }

        private static List<string> GetVideoPath()
        {
            return _camera.GetResizedVideoPath(startTime, endTime);
        }
        static void Testcamera()
        {
            DateTime a = DateTime.Now.AddMinutes(-3);
            DateTime b = DateTime.Now.AddMinutes(-2);
            var cam = new CameraServices.CameraService()
            {
                CameraPort = 8000,
                CameraIP = "192.168.1.168",
                CameraChannel = 45
            };
            var result = cam.GetResizedVideoPath(a, b);
            Console.WriteLine(result[0]);
        }
        static void TestUpVideo()
        {
            var videoUpload = GetVideo();
            videoUpload.VideoPaths = GetVideoPath();
            var up = APIService.UploadVideo(videoUpload).Result.IsSuccess;
            if (up)
            {
                Console.WriteLine("Thanh cong");
            }
            else
            {
                Console.WriteLine("Loi up video");
            }
        }

        static CameraServices.CameraService GetCamera()
        {
            var camera = new CameraServices.CameraService()
            {
                CameraPort = 8000,
                CameraIP = ip,
                CameraChannel = 36
            };
            return camera;
        }

        static Video GetVideo()
        {
            return new Video
            {
                Code = $"vid" + CurrentOrder.OrderCode,
                OrderId = CurrentOrder.OrderId,
                StartTime = startTime,
                EndTime = endTime,
                CameraCode = "CAM001"
            };
        }


    }
}
