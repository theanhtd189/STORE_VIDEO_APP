using ApiServices;
using CameraServices;
using Common;
using Common.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        /// Danh sách chứa thông tin về camera và service tương ứng với camera đó
        /// </summary>
        private static Dictionary<Camera, CameraServices.CameraService> _listCamera = new Dictionary<Camera, CameraServices.CameraService>();

        /// <summary>
        /// Có xóa file sau khi upload thành công không
        /// </summary>
        private bool _isDeleteFileAfterUploaded = AppConfig.GetBooleanValue("IsDeleteFileAfterUploaded");

        private bool _isProcessingVideo = false;
        public VideoMainService()
        {
            VideoLogger.Info("Start application - video service");
            InitializeEvent();
            ProcessDataInQueueAsync();
        }

        async Task StartService()
        {
            await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
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
                                Video videoUpload = new Video
                                {
                                    Code = $"vid_{order.OrderCode}",
                                    OrderCode = order.OrderCode,
                                    OrderId = order.OrderId,
                                    StartTime = order.StartTime,
                                    EndTime = order.EndTime,
                                    CameraCode = session.Cameras?.FirstOrDefault().Code,
                                    ListCamera = session.Cameras,
                                    VideoPaths = new List<string>(),
                                };

                                AddSourceToVideoModel(videoUpload, session.Cameras);


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
        void AddSourceToVideoModel(Video video, List<Camera> listCamera)
        {
            try
            {
                foreach (Camera camera in listCamera)
                {
                    CameraServices.CameraService currentCameraService = null;
                    var searchCamera = _listCamera.SingleOrDefault(x => x.Key.CameraIP == camera.CameraIP && x.Key.CameraChannel == camera.CameraChannel && x.Key.Code == camera.Code);
                    if (searchCamera.Key != null && searchCamera.Value != null)
                    {
                        currentCameraService = searchCamera.Value;
                    }
                    else
                    {
                        currentCameraService = GetCameraService(camera);
                        _listCamera.Add(camera, currentCameraService);
                    }

                    if (currentCameraService != null)
                    {
                        VideoLogger.Info($"Start getting video => OrderCode = {video.OrderCode}");
                        var paths = currentCameraService.GetResizedVideoPath(video.StartTime, video.EndTime);
                        video.VideoPaths.AddRange(paths);
                    }
                }
            }
            catch (Exception ex)
            {
                VideoLogger.Error(ex);
            }
        }

        void TestCamera()
        {
            DateTime a = DateTime.Now.AddMinutes(-2).AddSeconds(-20);
            DateTime b = DateTime.Now.AddMinutes(-2).AddSeconds(-1);
            var cam = new CameraServices.CameraService()
            {
                CameraPort = 8000,
                CameraIP = "192.168.1.168",
                CameraChannel = 45
            };

            var result = cam.GetResizedVideoPath(a, b);
        }

        /// <summary>
        /// Service hỗ trợ lấy video từ API Camera
        /// </summary>
        public CameraServices.CameraService GetCameraService(Camera camera)
        {
            try
            {
                return new CameraServices.CameraService()
                {
                    Code = camera.Code,
                    CameraIP = camera.CameraIP,
                    CameraPort = (short)camera.CameraPort,
                    CameraChannel = (short)camera.CameraChannel,
                };
            }
            catch (Exception ex)
            {
                VideoLogger.Error($"Camera Service: {ex.Message}");
                return null;
            }
        }
    }
}
