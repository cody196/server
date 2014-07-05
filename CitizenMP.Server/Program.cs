using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NPSharp.NP;

namespace CitizenMP.Server
{
    class Program
    {
        private async Task Start(string configFileName)
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

            var platformServer = config.PlatformServer ?? "iv-platform.prod.citizen.re";
            var client = new NPClient(platformServer, (config.PlatformPort == 0) ? (ushort)3036 : (ushort)config.PlatformPort);
            var connectResult = client.Connect();

            if (!connectResult)
            {
                this.Log().Fatal("Could not connect to the configured platform server ({0}).", platformServer);
                return;
            }

            // authenticate anonymously
            var task = client.AuthenticateWithLicenseKey("");

            if (!task.Wait(15000))
            {
                this.Log().Fatal("Could not authenticate anonymously to the configured platform server ({0}) - operation timed out.", platformServer);
                return;
            }

            if (!task.Result)
            {
                this.Log().Fatal("Could not authenticate anonymously to the configured platform server ({0}).", platformServer);
                return;
            }

            var commandManager = new Commands.CommandManager();
            var resManager = new Resources.ResourceManager();

            // create the game server (as resource scanning needs it now)
            var gameServer = new Game.GameServer(config, resManager, commandManager, client);

            // scan resources
            resManager.ScanResources("resources/");

            // start the game server
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
            new Program().Start((args.Length > 0) ? args[0] : null).Wait();
        }
    }
}
