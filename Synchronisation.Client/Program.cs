using Synchronisation.Core;
using System;
using M2I.Diagnostics;
using M2I.Diagnostics.EventLog;

namespace Synchronisation.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Loggers.AvaillableLoggers.Add(new ConsoleLogger());
            Loggers.AvaillableLoggers.Add(new FileLogger()
            {
                Source = ".\\logfile.log"
            });
            Loggers.AvaillableLoggers.Add(new EventLogger()
            {
                Name = "Synchronization Service",
                Source = "Synchronization Service"
            });

            FileSyncService service = new FileSyncService(@"C:\TMP\INPUT", "C:\\TMP\\OUTPUT", "TwoWaySourceFirst");

            service.Start();

            Console.ReadLine();

            service.Pause();

            Console.ReadLine();

            service.Continue();

            Console.ReadLine();

            service.Stop();

            Console.ReadLine();
        }
    }
}
