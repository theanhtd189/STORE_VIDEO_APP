using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoServices
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            new VideoMainService();
            Console.ReadLine();
        }
    }
}
