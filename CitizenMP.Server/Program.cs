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

                if (config.Downloads == null)
                {
                    config.Downloads = new Dictionary<string, DownloadConfiguration>();
                }
            }
            catch (System.IO.IOException)
            {
                this.Log().Fatal("Could not open the configuration file {0}.", configFileName ?? "citmp-server.yml");
                return;
            }

            var platformServer = config.PlatformServer ?? "iv-platform.prod.citizen.re";
            var client = new NPClient(platformServer, (config.PlatformPort == 0) ? (ushort)3036 : (ushort)config.PlatformPort);

            this.Log().Info("Connecting to Terminal platform server at {0}.", platformServer);

            var connectResult = client.Connect();

            if (!connectResult)
            {
                this.Log().Fatal("Could not connect to the configured platform server ({0}).", platformServer);
                return;
            }

            this.Log().Info("Authenticating to Terminal with anonymous license key.");

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

            this.Log().Info("Creating initial server instance.");

            var commandManager = new Commands.CommandManager();
            var resManager = new Resources.ResourceManager(config);

            // create the game server (as resource scanning needs it now)
            var gameServer = new Game.GameServer(config, resManager, commandManager, client);

            // preparse resources
            if (config.PreParseResources != null)
            {
                this.Log().Info("Pre-parsing resources: {0}", string.Join(", ", config.PreParseResources));

                foreach (var resource in config.PreParseResources)
                {
                    resManager.ScanResources("resources/", resource);

                    var res = resManager.GetResource(resource);

                    if (res != null)
                    {
                        await res.Start();
                    }
                }
            }

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
                    await res.Start();
                }
            }

            // start synchronizing the started resources
            resManager.StartSynchronization();

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
            // if running on WinNT
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Logging.WindowedLogger.Initialize();
            }

            Logging.BaseLog.SetStripSourceFilePath();

            try
            {
                // start the program
                new Program().Start((args.Length > 0) ? args[0] : null).Wait();

                Environment.Exit(0);
            }
            catch (AggregateException e)
            {
                Console.WriteLine(e.InnerException.ToString());

                Environment.Exit(1);
            }
        }
    }
}
