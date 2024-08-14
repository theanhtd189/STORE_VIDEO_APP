﻿using ApiServices;
using CameraServices;
using Common;
using Common.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace VideoServices
{
    public partial class VideoMainService
    {
        /// <summary>
        /// Tên của Pipe Service đích 
        /// </summary>
        private static readonly string _serverPipeName = AppConfig.GetStringValue("ServerPipeName")?? "StoreVideo_PipeService";

        /// <summary>
        /// Số giây (s) delay mỗi khi chạy 1 job 
        /// </summary>
        private static readonly int _delayJobTime = AppConfig.GetIntValue("DelayJobTime");

        /// <summary>
        /// Số giây (s) delay để thực hiện lại job khi job đó bị lỗi 
        /// </summary>
        private static readonly int _delayErrorJobTime = AppConfig.GetIntValue("DelayErrorJobTime");


        /// <summary>
        /// Danh sách các job cần được thực hiện
        /// </summary>
        private static Queue<Session> _queueMain = new Queue<Session>();

        /// <summary>
        /// Có xóa file sau khi upload thành công không
        /// </summary>
        private bool _isDeleteFileAfterUploaded = AppConfig.GetBooleanValue("IsDeleteFileAfterUploaded");

        private bool _isProcessingVideo = false;

        /// <summary>
        /// Service hỗ trợ lấy video từ API Camera
        /// </summary>
        private CameraService _cameraService;

        public VideoMainService()
        {
            VideoLogger.Info("Start application - video service");
            InitializeEvent();
            InitializeCameraService();
            ProcessDataInQueueAsync();
            //TestCamera();
        }

        private void InitializeCameraService()
        {
            try
            {
                string cameraHost = AppConfig.GetStringValue("CameraAdminHost");
                int cameraPort = AppConfig.GetIntValue("CameraAdminPort");
                _cameraService = new CameraService(cameraHost, cameraPort);
                _cameraService.LoginSystem();
            }
            catch (Exception ex)
            {
                MainLogger.Error("InitializeCameraService ex");
                MainLogger.Error(ex);
            }
        }


        /// <summary>
        /// Xử lý các job trong hàng đợi
        /// </summary>
        private void ProcessDataInQueueAsync()
        {
            while (true)
            {
                try
                {
                    while (_queueMain.Count > 0 && _isProcessingVideo == false)
                    {
                        var session = _queueMain.Dequeue();
                        Order order = session.CurrentOrder;
                        if (order != null)
                        {
                            try
                            {
                                _isProcessingVideo = true;
                                Camera camera = session.Cameras?.FirstOrDefault();
                                if (camera != null)
                                {
                                    _cameraService.Code = camera.Code;
                                    _cameraService.CameraChannel = (short)camera.CameraChannel;
                                }
                                else
                                {
                                    VideoLogger.Warn("ProcessDataInQueueAsync => Camera=NULL");
                                }

                                //Video model để upload api
                                Video videoUpload = new Video
                                {
                                    Code = $"vid_{order.OrderCode}",
                                    OrderCode = order.OrderCode,
                                    OrderId = order.OrderId,
                                    StartTime = order.StartTime,
                                    EndTime = order.EndTime,
                                    CameraCode = camera.Code,
                                    ListCamera = session.Cameras,
                                    VideoPaths = new List<string>(),
                                };

                                var videoPaths = _cameraService.GetResizedVideoPath(order.StartTime, order.EndTime);
                                videoUpload.VideoPaths.AddRange(videoPaths);


                                //Call API upload
                                if (videoUpload.VideoPaths.Count > 0)
                                {
                                    var up = APIService.UploadVideo(videoUpload).Result.IsSuccess;
                                    if (up)
                                    {
                                        _isProcessingVideo = false;//Done process video
                                        //call api thành công
                                        //xoa file
                                        if (_isDeleteFileAfterUploaded)
                                        {
                                            foreach (string path in videoUpload.VideoPaths)
                                            {
                                                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                                {
                                                    // Clean up
                                                    File.Delete(path);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Lỗi call api up file
                                        _queueMain.Enqueue(session);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                VideoLogger.Error("Lỗi call api up video " + ex.Message);
                                Thread.Sleep(_delayErrorJobTime * 1000);
                                _queueMain.Enqueue(session);
                            }
                        }
                        else
                        {
                            //Sao lại truyền order null vào đây?
                            //log vào để check nguyên nhân
                            VideoLogger.Error("Lỗi nhận data từ main service");
                            VideoLogger.Error("ORDER NULL");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // handle exception
                    VideoLogger.Error("ProcessDataInQueue exception " + ex.Message);
                }
                finally
                {
                    Thread.Sleep(_delayJobTime * 1000);
                }
            }
        }

        /// <summary>
        /// Lấy video đã render được để gắn vào model api 
        /// </summary>
        /// <param name="video"></param>
        /// <param name="listCamera"></param>
        //void AddSourceToVideoModel(Video video, List<Camera> listCamera)
        //{
        //    try
        //    {
        //        foreach (Camera camera in listCamera)
        //        {
        //            CameraServices.CameraService currentCameraService = null;
        //            var searchCamera = _listCamera.SingleOrDefault(x => x.Key.CameraIP == camera.CameraIP && x.Key.CameraChannel == camera.CameraChannel && x.Key.Code == camera.Code);
        //            if (searchCamera.Key != null && searchCamera.Value != null)
        //            {
        //                currentCameraService = searchCamera.Value;
        //            }
        //            else
        //            {
        //                currentCameraService = GetCameraService(camera);
        //                _listCamera.Add(camera, currentCameraService);
        //            }

        //            if (currentCameraService != null)
        //            {
        //                VideoLogger.Info($"Start getting video => OrderCode = {video.OrderCode}");
        //                var paths = currentCameraService.GetResizedVideoPath(video.StartTime, video.EndTime);
        //                video.VideoPaths.AddRange(paths);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        VideoLogger.Error(ex);
        //    }
        //}

        void TestCamera()
        {
            DateTime a = DateTime.Now.AddMinutes(-2).AddSeconds(-10);
            DateTime b = DateTime.Now.AddMinutes(-2).AddSeconds(-1);         
            var result = _cameraService.GetResizedVideoPath(a,b);
            Console.WriteLine(result[0]);
        }

        /// <summary>
        /// Service hỗ trợ lấy video từ API Camera
        /// </summary>
        //public CameraServices.CameraService GetCameraService(Camera camera)
        //{
        //    try
        //    {
        //        return new CameraServices.CameraService()
        //        {
        //            Code = camera.Code,
        //            CameraIP = camera.CameraIP,
        //            CameraPort = (short)camera.CameraPort,
        //            CameraChannel = (short)camera.CameraChannel,
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        VideoLogger.Error($"Camera Service: {ex.Message}");
        //        return null;
        //    }
        //}
    }
}
