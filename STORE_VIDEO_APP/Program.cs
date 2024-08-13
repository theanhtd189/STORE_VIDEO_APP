using System;
using System.Text;
using System.Threading;

namespace STORE_VIDEO_APP
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            new AppMainService();
            Console.ReadLine();
        }
    }
}
