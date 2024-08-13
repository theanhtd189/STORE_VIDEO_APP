using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Common.Model
{
    [Serializable]
    public class Scanner
    {      
        private string _scannerCode;

        public string ScannerCode
        {
            get { return _scannerCode; }
            set { _scannerCode = value; }
        }
    }

    [Serializable]
    public class Camera
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string DeskId { get; set; }
        public string DeskCode { get; set; }

        private string _cameraIP;
        public string CameraIP
        {
            get { return _cameraIP; }
            set
            {
                var splitIP = value.Split(':');
                if (splitIP.Length > 1)
                {
                    if (!string.IsNullOrEmpty(splitIP[0]))
                    {
                        this._cameraIP = splitIP[0];
                    }
                    if (!string.IsNullOrEmpty(splitIP[1]))
                    {
                        this.CameraPort = short.Parse(splitIP[1]);
                    }
                }
                else
                {
                    _cameraIP = value;
                }
            }
        }
        public int CameraChannel
        {
            get;
            set;
        }
        public int CameraPort
        {
            get;
            set;
        }
    }

    [Serializable]
    public class Order
    {
        private string _orderId;

        [JsonProperty("orderId")]
        public string OrderId
        {
            get { return _orderId; }
            set { _orderId = value; }
        }
        private string _orderCode;

        [JsonProperty("orderCode")]
        public string OrderCode
        {
            get { return _orderCode; }
            set { _orderCode = value; }
        }

        [JsonProperty("start")]
        public long Start { get; set; }

        [JsonProperty("end")]
        public long End { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int Status { get; set; }

        public string Note { get; set; }

        public string UserId { get; set; }

        [JsonProperty("deskId")]
        public int DeskId { get; set; }
    }

    [Serializable]
    public class User
    {
        private string _userId;

        public string UserId
        {
            get { return _userId; }
            set { _userId = value; }
        }

        private string _deskCode;

        public string DeskCode
        {
            get { return _deskCode; }
            set { _deskCode = value; }
        }
        private int _deskId;

        public int DeskId
        {
            get { return _deskId; }
            set { _deskId = value; }
        }
    }

    [Serializable]
    public class Session : ICloneable
    {
        public User User { get; set; }
        public Scanner Scanner { get; set; }
        public Order CurrentOrder { get; set; }
        public List<Camera> Cameras { get; set; }
        public object Clone()
        {
            var clone = new Session
            {
                User = this.User,
                Scanner = this.Scanner,
                CurrentOrder = this.CurrentOrder,
                Cameras = this.Cameras,
            };
            return clone;
        }
    }

    [Serializable]
    public class Video
    {
        public string Code { get; set; }
        public List<string> VideoPaths { get; set; }
        public string Note { get; set; }
        public string CameraCode { get; set; }
        public List<Camera> ListCamera { get; set; }
        public string OrderId { get; set; }
        public string OrderCode { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }


    [Serializable]
    public class QRData
    {
        public string UserId { get; set; }
        public int DeskId { get; set; }
        public string Command { get; set; }
        public List<Camera> Cameras { get; set; }
        public string OrderCode { get; set; }
        public override string ToString()
        {
            try
            {
                return JsonConvert.SerializeObject(this, Formatting.Indented);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    [Serializable]
    public class Account
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        public string ReturnUrl { get; set; }
    }
}
