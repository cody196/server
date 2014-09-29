using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace CitizenMP.Server.Resources
{
    public class Resource
    {
        public string Name { get; private set; }
        public string Path { get; private set; }
        public ResourceState State { get; private set; }
        public Dictionary<string, string> Info { get; private set; }
        public List<string> Dependencies { get; private set; }
        public List<string> Exports { get; private set; }
        public List<string> Scripts { get; private set; }
        public List<string> AuxFiles { get; private set; }
        public List<string> Dependants { get; private set; }
        public List<string> ServerScripts { get; private set; }
        public Dictionary<string, FileInfo> ExternalFiles { get; private set; }

        public DownloadConfiguration DownloadConfiguration { get; set; }

        public ResourceManager Manager { get; set; }

        public string ClientPackageHash { get; internal set; }

        private FileSystemWatcher m_watcher;
        private ScriptEnvironment m_scriptEnvironment;

        public Resource(string name, string path)
        {
            Name = name;
            Path = path;
            State = ResourceState.Stopped;
            Dependants = new List<string>();
            StreamEntries = new Dictionary<string, StreamCacheEntry>();
            ExternalFiles = new Dictionary<string, FileInfo>();
        }

        public bool Parse()
        {
            if (!EnsureScriptEnvironment())
            {
                return false;
            }

            State = ResourceState.Parsing;

            Info = new Dictionary<string, string>();
            Dependencies = new List<string>();
            Exports = new List<string>();
            Scripts = new List<string>();
            AuxFiles = new List<string>();
            ServerScripts = new List<string>();

            var result = ParseInfoFile();

            State = ResourceState.Stopped;

            return result;
        }

        private bool EnsureScriptEnvironment()
        {
            if (m_scriptEnvironment == null)
            {
                m_scriptEnvironment = new ScriptEnvironment(this);

                if (!m_scriptEnvironment.Create())
                {
                    this.Log().Error("Resource {0} caused an error during loading. Please see the above lines for details.", Name);

                    State = ResourceState.Error;
                    return false;
                }
            }

            return true;
        }

        private bool ParseInfoFile()
        {
            return m_scriptEnvironment.DoInitFile(true);
        }

        public async Task<bool> Start()
        {
            if (State == ResourceState.Running)
            {
                return true;
            }

            if (State != ResourceState.Stopped && State != ResourceState.Starting)
            {
                throw new InvalidOperationException("can not start a resource that is not stopped");
            }

            this.Log().Info("Starting resource {0} (last state: {1}).", Name, State);

            // as this is already done for us in this case
            if (State != ResourceState.Starting)
            {
                if (!Parse())
                {
                    State = ResourceState.Error;

                    return false;
                }

                // resolve dependencies
                foreach (var dep in Dependencies)
                {
                    var res = Manager.GetResource(dep);

                    if (res == null)
                    {
                        this.Log().Warn("Can't resolve dependency {0} from resource {1}.", dep, Name);
                        return false;
                    }

                    await res.Start();
                    res.AddDependant(Name);
                }

                // execute tasks
                var runner = new Tasks.ResourceTaskRunner();

                if (!await runner.ExecuteTasks(this))
                {
                    this.Log().Error("Executing tasks for resource {0} failed.", Name);

                    State = ResourceState.Error;
                    return false;
                }

                m_watcher = new FileSystemWatcher();

                // create script environment
                if (!EnsureScriptEnvironment())
                {
                    return false;
                }
            }

            State = ResourceState.Starting;

            m_scriptEnvironment.DoInitFile(false);

            // trigger event
            if (!Manager.TriggerEvent("onResourceStarting", -1, Name))
            {
                // how the h-
                if (State == ResourceState.Running)
                {
                    return true;
                }

                Stop();

                return false;
            }

            m_scriptEnvironment.LoadScripts();

            // TODO: add development mode check
            m_watcher.Path = Path;
            m_watcher.IncludeSubdirectories = true;
            m_watcher.NotifyFilter = NotifyFilters.LastWrite;
            m_watcher.Changed += (s, e) => InvokeUpdateClientPackage();
            m_watcher.Created += (s, e) => InvokeUpdateClientPackage();
            m_watcher.Deleted += (s, e) => InvokeUpdateClientPackage();
            m_watcher.Renamed += (s, e) => InvokeUpdateClientPackage();

            m_watcher.EnableRaisingEvents = true;

            State = ResourceState.Running;

            // trigger event
            if (!Manager.TriggerEvent("onResourceStart", -1, Name))
            {
                this.Log().Info("Resource start canceled by event.");

                Stop();

                return false;
            }

            // broadcast to current clients
            var clients = ClientInstances.Clients.Where(c => c.Value.NetChannel != null).Select(c => c.Value);

            foreach (var client in clients)
            {
                client.SendReliableCommand(0xAFE4CD4A, Encoding.UTF8.GetBytes(Name)); // msgResStart
            }

            return true;
        }

        public void RemoveDependant(string name)
        {
            Dependants.Remove(name);
        }

        public bool Stop()
        {
            if (State != ResourceState.Running && State != ResourceState.Starting)
            {
                throw new InvalidOperationException(string.Format("Tried to stop a resource ({0}) that wasn't running.", Name));
            }

            this.Log().Info("Stopping resource {0} (last state: {1}).", Name, State);

            if (State == ResourceState.Running)
            {
                if (!Manager.TriggerEvent("onResourceStop", -1, Name))
                {
                    return false;
                }
            }

            var dependants = Dependants.GetRange(0, Dependants.Count);

            foreach (var dependant in dependants)
            {
                var dependantResource = Manager.GetResource(dependant);

                dependantResource.Stop();
            }

            Dependants.Clear();

            foreach (var dependency in Dependencies)
            {
                var dependencyResource = Manager.GetResource(dependency);

                dependencyResource.RemoveDependant(Name);
            }

            // remove the watcher
            m_watcher.Dispose();
            m_watcher = null;

            // dispose of the script environment
            m_scriptEnvironment.Dispose();
            m_scriptEnvironment = null;

            if (State == ResourceState.Running)
            {
                // broadcast a stop message to all clients
                var clients = ClientInstances.Clients.Where(c => c.Value.NetChannel != null).Select(c => c.Value);

                foreach (var client in clients)
                {
                    client.SendReliableCommand(0x45E855D7, Encoding.UTF8.GetBytes(Name)); // msgResStop
                }
            }

            // done!
            State = ResourceState.Stopped;

            return true;
        }

        public bool HasRef(int reference, uint instance)
        {
            return (m_scriptEnvironment != null && m_scriptEnvironment.HasRef(reference) && m_scriptEnvironment.InstanceID == instance);
        }

        public void Tick()
        {
            if (m_scriptEnvironment != null)
            {
                m_scriptEnvironment.Tick();
            }
        }

        public Delegate GetRef(int reference)
        {
            return m_scriptEnvironment.GetRef(reference);
        }

        public string CallRef(Delegate method, string argsSerialized)
        {
            return m_scriptEnvironment.CallExport(method, argsSerialized);
        }

        public void RemoveRef(int luaRef)
        {
            m_scriptEnvironment.RemoveRef(luaRef);
        }

        private static bool ms_clientUpdateQueued;

        private void InvokeUpdateClientPackage()
        {
            if (ms_clientUpdateQueued)
            {
                return;
            }

            ms_clientUpdateQueued = true;

            Task.Run(async () =>
            {
                await Task.Delay(500);

                // go
                var runner = new Tasks.ResourceTaskRunner();
                runner.ExecuteTasks(this);

                // and unlock the lock
                ms_clientUpdateQueued = false;
            });
        }

        public void AddDependant(string name)
        {
            Dependants.Add(name);
        }

        public class StreamCacheEntry
        {
            public string BaseName { get; set; }
            public string HashString { get; set; }
            public string FileName { get; set; }
            public uint RscFlags { get; set; }
            public uint RscVersion { get; set; }
            public uint Size { get; set; }
        }

        public IDictionary<string, StreamCacheEntry> StreamEntries { get; set; }

        public bool IsSynchronizing { get; set; }

        public Stream OpenClientPackage()
        {
            return File.Open("cache/http-files/" + Name + ".rpf", FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public FileInfo GetClientPackageInfo()
        {
            return new FileInfo("cache/http-files/" + Name + ".rpf");
        }

        public IEnumerable<FileInfo> GetStreamFilesInfo()
        {
            return StreamEntries.Where(e => e.Value.FileName != null).Select(e => new FileInfo(e.Value.FileName));
        }

        public Stream GetStreamFile(string baseName)
        {
            StreamCacheEntry entry;

            if (!StreamEntries.TryGetValue(baseName, out entry))
            {
                return null;
            }

            if (entry.FileName == null)
            {
                return null;
            }

            return File.Open(entry.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void TriggerEvent(string eventName, string argsSerialized, int source)
        {
            if (State == ResourceState.Running)
            {
                m_scriptEnvironment.TriggerEvent(eventName, argsSerialized, source);
            }
        }

        private class ResourceData
        {
            public Dictionary<string, string> info { get; set; }

            public List<string> exports { get; set; }

            public List<string> dependencies { get; set; }

            public List<string> scripts { get; set; }

            public List<string> auxFiles { get; set; }

            public List<string> serverScripts { get; set; }
        }
    }

    public enum ResourceState
    {
        Stopped,
        Stopping,
        Starting,
        Running,
        Parsing,
        Error
    }
}
