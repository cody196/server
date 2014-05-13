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
            /*var res = new Resources.Resource("lovely", @"S:\Games\Steam\steamapps\common\grand theft auto iv\GTAIV\citizen\lovely");
            res.Parse();
            res.Start();*/

            var resManager = new Resources.ResourceManager();
            resManager.ScanResources("resources/");

            // initialize the HTTP server
            var httpServer = new HTTP.HttpServer(resManager);
            httpServer.Start();

            // and the game server
            var gameServer = new Game.GameServer(resManager);
            gameServer.Start();

            // start resources
            resManager.GetResource("gameInit").Start();
            resManager.GetResource("lovely").Start();

            resManager.TriggerEvent("dick", -1, 30, 45, 1911);

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
