using STORE_VIDEO_APP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using VideoServices;

namespace AppService
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Task appService = Task.Run(() =>
            {
                new AppMainService();
            });
            Task videoService = Task.Run(() =>
            {
                new VideoMainService();
            });
            Task.WaitAll(appService, videoService);
        }

        protected override void OnStop()
        {
        }
    }
}
