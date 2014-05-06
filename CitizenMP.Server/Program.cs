using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CitizenMP.Server
{
    class Program
    {
        private void Start()
        {
            // initialize the HTTP server
            var httpServer = new HTTP.HttpServer();
            httpServer.Start();

            // and the game server
            var gameServer = new Game.GameServer();
            gameServer.Start();

            // main loop
            int lastTickCount = Environment.TickCount;

            while (true)
            {
                Thread.Sleep(5);

                var tc = Environment.TickCount;

                gameServer.Tick(tc - lastTickCount);

                lastTickCount = tc;
            }
        }

        static void Main(string[] args)
        {
            // initialize the logging subsystem
            LoggingExtensions.Logging.Log.InitializeWith<LoggingExtensions.NLog.NLogLog>();

            // start the program
            new Program().Start();
        }
    }
}
