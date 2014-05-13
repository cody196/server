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
            try
            {
                // create path and read the file
                var infoPath = System.IO.Path.Combine(Path, "info.yml");

                var buffer = File.ReadAllText(infoPath);

                var deserializer = new Deserializer(ignoreUnmatched: true);
                var data = deserializer.Deserialize<ResourceData>(new StringReader(buffer));

                Info = data.info ?? new Dictionary<string, string>();
                Dependencies = data.dependencies ?? new List<string>();
                Exports = data.exports ?? new List<string>();
                Scripts = data.scripts ?? new List<string>();
                AuxFiles = data.auxFiles ?? new List<string>();
                ServerScripts = data.serverScripts ?? new List<string>();

                return true;
            }
            catch (FileNotFoundException)
            {
                this.Log().Error("Could not find the info file for resource {0}.", Name);
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Could not parse resource information for resource " + Name + ".", e);
            }

            return false;
        }

        public void Start()
        {
            if (State == ResourceState.Running)
            {
                return;
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
                    return;
                }

                res.Start();
                res.AddDependant(Name);
            }

            if (!UpdateClientPackage())
            {
                this.Log().Error("Couldn't update the client package.");

                State = ResourceState.Error;
                return;
            }

            // create script environment
            m_scriptEnvironment = new ScriptEnvironment(this);

            if (!m_scriptEnvironment.Create())
            {
                this.Log().Error("Resource {0} caused an error during loading. Please see the above lines for details.", Name);

                State = ResourceState.Error;
                return;
            }

            // TODO: add development mode check
            m_watcher = new FileSystemWatcher();
            m_watcher.Path = Path;
            m_watcher.IncludeSubdirectories = true;
            m_watcher.NotifyFilter = NotifyFilters.LastWrite;
            m_watcher.Changed += (s, e) => UpdateClientPackage();
            m_watcher.Created += (s, e) => UpdateClientPackage();
            m_watcher.Deleted += (s, e) => UpdateClientPackage();
            m_watcher.Renamed += (s, e) => UpdateClientPackage();

            m_watcher.EnableRaisingEvents = true;

            State = ResourceState.Running;
        }

        public void AddDependant(string name)
        {
            Dependants.Add(name);
        }

        private bool UpdateClientPackage()
        {
            try
            {
                var requiredFiles = new List<string>() { "info.yml" };

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
                    requiredFiles.ForEach(a => rpf.AddFile(a, File.ReadAllBytes(System.IO.Path.Combine(Path, a))));
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
