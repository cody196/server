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
            StreamEntries = new Dictionary<string, StreamCacheEntry>();
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

            if (!UpdateStreamFiles())
            {
                this.Log().Error("Couldn't update streamed files.");

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

        public string CallRef(int luaRef, string argsSerialized)
        {
            return m_scriptEnvironment.CallExport(luaRef, argsSerialized);
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
                UpdateClientPackage();

                // and unlock the lock
                ms_clientUpdateQueued = false;
            });
        }

        public void AddDependant(string name)
        {
            Dependants.Add(name);
        }

        private bool UpdateStreamFiles()
        {
            var streamFolder = System.IO.Path.Combine(Path, "stream"); 

            if (!Directory.Exists(streamFolder))
            {
                return true;
            }

            var streamFiles = Directory.GetFiles(streamFolder, "*.*", SearchOption.AllDirectories);
            var streamCacheFile = string.Format("cache/http-files/{0}.sfl", Name);
            var needsUpdate = false;

            if (!File.Exists(streamCacheFile))
            {
                needsUpdate = true;
            }

            if (!needsUpdate)
            {
                var modDate = streamFiles.Select(a => File.GetLastWriteTime(a)).OrderByDescending(a => a).First();
                var cacheModDate = File.GetLastWriteTime(streamCacheFile);

                if (modDate > cacheModDate)
                {
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                return CreateStreamCacheList(streamFiles, streamCacheFile);
            }
            else
            {
                return LoadStreamCacheList(streamFiles, streamCacheFile);
            }
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

        private bool CreateStreamCacheList(string[] files, string cacheFilename)
        {
            JArray cacheOutList = new JArray();

            foreach (var file in files)
            {
                var hash = Utils.GetFileSHA1String(file);
                var basename = System.IO.Path.GetFileName(file);

                var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var reader = new BinaryReader(stream);

                var size = stream.Length;
                var resourceFlags = size;
                var resourceVersion = 0;

                if (reader.ReadUInt32() == 0x05435352) // RSC\x5
                {
                    resourceVersion = reader.ReadInt32();
                    resourceFlags = reader.ReadUInt32();
                }

                var obj = new JObject();
                obj["Hash"] = hash;
                obj["BaseName"] = basename;
                obj["Size"] = size;
                obj["RscFlags"] = resourceFlags;
                obj["RscVersion"] = resourceVersion;

                cacheOutList.Add(obj);
            }

            File.WriteAllText(cacheFilename, cacheOutList.ToString());

            LoadStreamCacheList(files, cacheFilename);

            return true;
        }

        private bool LoadStreamCacheList(string[] files, string cacheFile)
        {
            var cacheList = JArray.Parse(File.ReadAllText(cacheFile));
            var cacheEntries = new Dictionary<string, StreamCacheEntry>();

            foreach (var entry in cacheList)
            {
                var obj = entry as JObject;

                if (obj == null)
                {
                    continue;
                }

                var newEntry = new StreamCacheEntry();
                newEntry.BaseName = obj.Value<string>("BaseName");
                newEntry.HashString = obj.Value<string>("Hash");
                newEntry.RscFlags = obj.Value<uint>("RscFlags");
                newEntry.RscVersion = obj.Value<uint>("RscVersion");
                newEntry.Size = obj.Value<uint>("Size");

                cacheEntries.Add(obj.Value<string>("BaseName"), newEntry);
            }

            foreach (var file in files)
            {
                var basename = System.IO.Path.GetFileName(file);

                if (!cacheEntries.ContainsKey(basename)) { continue; }

                cacheEntries[basename].FileName = file;
            }

            StreamEntries = cacheEntries;

            return true;
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

        public Stream GetStreamFile(string baseName)
        {
            StreamCacheEntry entry;

            if (!StreamEntries.TryGetValue(baseName, out entry))
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
        Running,
        Error
    }
}
