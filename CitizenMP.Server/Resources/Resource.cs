using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public ResourceManager Manager { get; set; }

        public string ClientPackageHash { get; private set; }

        private FileSystemWatcher m_watcher;
        private ScriptEnvironment m_scriptEnvironment;

        public Resource(string name, string path)
        {
            Name = name;
            Path = path;
            State = ResourceState.Stopped;
            Dependants = new List<string>();
        }

        public bool Parse()
        {
            if (!EnsureScriptEnvironment())
            {
                return false;
            }

            Info = new Dictionary<string, string>();
            Dependencies = new List<string>();
            Exports = new List<string>();
            Scripts = new List<string>();
            AuxFiles = new List<string>();
            ServerScripts = new List<string>();

            return ParseInfoFile();
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

        public bool Start()
        {
            if (State == ResourceState.Running)
            {
                return true;
            }

            if (State != ResourceState.Stopped)
            {
                throw new InvalidOperationException("can not start a resource that is not stopped");
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

                res.Start();
                res.AddDependant(Name);
            }

            m_watcher = new FileSystemWatcher();

            if (!UpdateClientPackage())
            {
                this.Log().Error("Couldn't update the client package.");

                State = ResourceState.Error;
                return false;
            }

            // create script environment
            if (!EnsureScriptEnvironment())
            {
                return false;
            }

            m_scriptEnvironment.DoInitFile(false);
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

        public bool Stop()
        {
            if (State != ResourceState.Running)
            {
                throw new InvalidOperationException("Tried to stop a resource that wasn't running.");
            }

            if (!Manager.TriggerEvent("onResourceStop", -1, Name))
            {
                return false;
            }

            foreach (var dependant in Dependants)
            {
                var dependantResource = Manager.GetResource(dependant);

                dependantResource.Stop();
            }

            // dispose of the script environment
            m_scriptEnvironment.Dispose();
            m_scriptEnvironment = null;

            // broadcast a stop message to all clients
            var clients = ClientInstances.Clients.Where(c => c.Value.NetChannel != null).Select(c => c.Value);

            foreach (var client in clients)
            {
                client.SendReliableCommand(0x45E855D7, Encoding.UTF8.GetBytes(Name)); // msgResStop
            }

            // done!
            State = ResourceState.Stopped;

            return true;
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
                UpdateClientPackage();

                // and unlock the lock
                ms_clientUpdateQueued = false;
            });
        }

        public void AddDependant(string name)
        {
            Dependants.Add(name);
        }

        private bool UpdateClientPackage()
        {
            lock (m_watcher)
            {
                try
                {
                    var requiredFiles = new List<string>() { "__resource.lua" };

                    // add all script files
                    requiredFiles.AddRange(Scripts);
                    requiredFiles.AddRange(AuxFiles);

                    // get the last-modified date of the current RPF and the cache
                    var rpfName = "cache/http-files/" + Name + ".rpf";

                    var modDate = requiredFiles.Select(a => System.IO.Path.Combine(Path, a)).Select(a => File.GetLastWriteTime(a)).OrderByDescending(a => a).First();
                    var rpfModDate = File.GetLastWriteTime(rpfName);

                    if (modDate > rpfModDate)
                    {
                        // write the RPF
                        if (!Directory.Exists("cache/http-files/"))
                        {
                            Directory.CreateDirectory("cache/http-files");
                        }

                        var rpf = new Formats.RPFFile();
                        requiredFiles.Where(a => File.Exists(System.IO.Path.Combine(Path, a))).ToList().ForEach(a => rpf.AddFile(a, File.ReadAllBytes(System.IO.Path.Combine(Path, a))));
                        rpf.Write(rpfName);
                    }

                    // and get the hash of the client package to store for ourselves (yes, we do this on every load; screw big RPF files, we're reading them anyway)
                    var hash = Utils.GetFileSHA1String(rpfName);

                    ClientPackageHash = hash;

                    return true;
                }
                catch (Exception e)
                {
                    this.Log().Error(() => "Couldn't update the client package for " + Name + " - " + e.Message, e);

                    return false;
                }
            }
        }

        public Stream OpenClientPackage()
        {
            return File.Open("cache/http-files/" + Name + ".rpf", FileMode.Open, FileAccess.Read, FileShare.Read);
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
        Running,
        Error
    }
}
