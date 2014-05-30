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
        private void Start(string configFileName)
        {
            Configuration config;

            try
            {
                config = Configuration.Load(configFileName ?? "citmp-server.yml");

                if (config.AutoStartResources == null)
                {
                    this.Log().Fatal("No auto-started resources were configured.");
                    return;
                }

                if (config.ListenPort == 0)
                {
                    this.Log().Fatal("No port was configured.");
                    return;
                }
            }
            catch (System.IO.IOException)
            {
                this.Log().Fatal("Could not open the configuration file {0}.", configFileName ?? "citmp-server.yml");
                return;
            }

            var resManager = new Resources.ResourceManager();
            resManager.ScanResources("resources/");

            // initialize the game server
            var gameServer = new Game.GameServer(config, resManager);
            gameServer.Start();

            // and initialize the HTTP server
            var httpServer = new HTTP.HttpServer(config, resManager);
            httpServer.Start();

            // start resources
            foreach (var resource in config.AutoStartResources)
            {
                var res = resManager.GetResource(resource);

                if (res == null)
                {
                    this.Log().Error("Could not find auto-started resource {0}.", resource);
                }
                else
                {
                    res.Start();
                }
            }

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
            new Program().Start((args.Length > 0) ? args[0] : null);
        }
    }
}
