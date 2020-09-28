using Synchronisation.Core;
using System;

namespace Synchronisation.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            FileSyncService service = new FileSyncService(@"C:\TMP\INPUT", "C:\\TMP\\OUTPUT", "TwoWaySourceFirst");

            service.Start();

            Console.ReadLine();

            service.Stop();

            Console.ReadLine();
        }
    }
}
