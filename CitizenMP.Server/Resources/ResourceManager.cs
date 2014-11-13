using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Resources
{
    public class ResourceManager
    {
        private Dictionary<string, Resource> m_resources;

        private Configuration m_configuration;

        public Configuration Configuration
        {
            get
            {
                return m_configuration;
            }
        }

        internal Game.GameServer GameServer { get; private set; }

        internal Game.RconLog RconLog { get; private set; }

        public ResourceManager(Configuration config)
        {
            m_resources = new Dictionary<string, Resource>();
            m_configuration = config;

            RconLog = new Game.RconLog();
        }

        internal void SetGameServer(Game.GameServer gameServer)
        {
            if (GameServer != null)
            {
                throw new InvalidOperationException("This manager is already associated with a game server.");
            }

            GameServer = gameServer;
        }

        public Resource GetResource(string name)
        {
            Resource res;

            if (m_resources.TryGetValue(name, out res))
            {
                return res;
            }

            return null;
        }

        public Resource AddResource(string name, string path)
        {
            if (m_resources.ContainsKey(name))
            {
                return null;
            }

            var res = new Resource(name, path);
            res.Manager = this;
            res.DownloadConfiguration = m_configuration.GetDownloadConfiguration(name);

            AddResource(res);

            if (res.Parse())
            {
                return res;
            }

            m_resources.Remove(res.Name);

            return null;
        }

        public IEnumerable<Resource> GetRunningResources()
        {
            return from r in m_resources
                   where r.Value.State == ResourceState.Running
                   select r.Value;
        }

        public void AddResource(Resource res)
        {
            m_resources[res.Name] = res;
        }

        public void ScanResources(string path, string onlyThisResource = null)
        {
            var subdirs = Directory.GetDirectories(path);

            foreach (var dir in subdirs)
            {
                var basename = Path.GetFileName(dir);

                if (basename[0] == '[')
                {
                    if (!basename.Contains(']'))
                    {
                        this.Log().Info("Ignored {0} - no end bracket", basename);
                        continue;
                    }

                    ScanResources(dir, onlyThisResource);
                }
                else
                {
                    if (onlyThisResource == null || onlyThisResource == basename)
                    {
                        if (onlyThisResource == null)
                        {
                            Console.Write(".");
                        }

                        this.Log().Info("Found resource {0} in {1}.", basename, dir);

                        AddResource(basename, dir);
                    }
                }
            }
        }

        public void ScanResources(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                ScanResources(path);
            }
        }

        public void Tick()
        {
            foreach (var resource in m_resources)
            {
                resource.Value.Tick();
            }
        }

        public bool TriggerEvent(string eventName, int source, params object[] args)
        {
            // convert the arguments to an object each
            var array = Utils.SerializeEvent(args);
            var sb = new StringBuilder(array.Length);

            foreach (var b in array)
            {
                sb.Append((char)b);
            }

            // and trigger the event
            return TriggerEvent(eventName, sb.ToString(), source);
        }

        [ThreadStatic]
        private Stack<bool> m_eventCancelationState = new Stack<bool>();

        [ThreadStatic]
        private bool m_eventCanceled;

        public bool TriggerEvent(string eventName, string argsSerialized, int source)
        {
            m_eventCancelationState.Push(false);

            foreach (var resource in m_resources)
            {
                resource.Value.TriggerEvent(eventName, argsSerialized, source);
            }

            m_eventCanceled = m_eventCancelationState.Pop();

            return !m_eventCanceled;
        }

        public bool WasEventCanceled()
        {
            return m_eventCanceled;
        }

        public void CancelEvent()
        {
            m_eventCancelationState.Pop();
            m_eventCancelationState.Push(true);
        }

        public void StartSynchronization()
        {
            Task.Run(async () =>
            {
                foreach (var resource in m_resources)
                {
                    if (resource.Value.State == ResourceState.Running)
                    {
                        var downloadConfig = m_configuration.GetDownloadConfiguration(resource.Key);

                        if (downloadConfig != null && !string.IsNullOrWhiteSpace(downloadConfig.UploadURL))
                        {
                            var syncProvider = new ResourceUpdater(resource.Value, downloadConfig);

                            await syncProvider.SyncResource();
                        }
                    }
                }
            });
        }
    }
}
