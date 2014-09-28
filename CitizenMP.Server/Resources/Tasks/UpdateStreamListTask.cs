using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.Resources.Tasks
{
    class UpdateStreamListTask : ResourceTask
    {
        public override IEnumerable<string> DependsOn
        {
            get { return new string[0]; }
        }

        public override bool NeedsExecutionFor(Resource resource)
        {
            // check if a premade resource cache exists
            var preCachePath = Path.Combine(resource.Path, "streamcache.sfl");

            if (File.Exists(preCachePath))
            {
                return LoadStreamCacheList(resource, null, preCachePath);
            }

            var streamFolder = Path.Combine(resource.Path, "stream");

            if (!Directory.Exists(streamFolder))
            {
                return false;
            }

            var streamFiles = Directory.GetFiles(streamFolder, "*.*", SearchOption.AllDirectories);
            var streamCacheFile = string.Format("cache/http-files/{0}.sfl", resource.Name);
            var needsUpdate = false;

            if (!File.Exists(streamCacheFile))
            {
                this.Log().Info("Generating stream cache list for {0} (no stream cache)", resource.Name);

                return true;
            }

            if (!needsUpdate)
            {
                var modDate = streamFiles.Select(a => File.GetLastWriteTime(a)).OrderByDescending(a => a).First();
                var cacheModDate = File.GetLastWriteTime(streamCacheFile);

                if (modDate > cacheModDate)
                {
                    this.Log().Info("Generating stream cache list for {0} (modification dates differ)", resource.Name);

                    return true;
                }
            }

            // load the existing stream cache
            LoadStreamCacheList(resource, streamFiles, streamCacheFile);

            return false;
        }

        public override bool Process(Resource resource)
        {
            var streamFolder = Path.Combine(resource.Path, "stream");
            var streamFiles = Directory.GetFiles(streamFolder, "*.*", SearchOption.AllDirectories);
            var streamCacheFile = string.Format("cache/http-files/{0}.sfl", resource.Name);

            return CreateStreamCacheList(resource, streamFiles, streamCacheFile);
        }

        private bool CreateStreamCacheList(Resource resource, string[] files, string cacheFilename)
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

            File.WriteAllText(cacheFilename, cacheOutList.ToString(Newtonsoft.Json.Formatting.None));

            LoadStreamCacheList(resource, files, cacheFilename);

            return true;
        }

        private bool LoadStreamCacheList(Resource resource, string[] files, string cacheFile)
        {
            var cacheList = JArray.Parse(File.ReadAllText(cacheFile));
            var cacheEntries = new Dictionary<string, Resource.StreamCacheEntry>();

            foreach (var entry in cacheList)
            {
                var obj = entry as JObject;

                if (obj == null)
                {
                    continue;
                }

                var newEntry = new Resource.StreamCacheEntry();
                newEntry.BaseName = obj.Value<string>("BaseName");
                newEntry.HashString = obj.Value<string>("Hash");
                newEntry.RscFlags = obj.Value<uint>("RscFlags");
                newEntry.RscVersion = obj.Value<uint>("RscVersion");
                newEntry.Size = obj.Value<uint>("Size");

                cacheEntries.Add(obj.Value<string>("BaseName"), newEntry);
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    var basename = System.IO.Path.GetFileName(file);

                    if (!cacheEntries.ContainsKey(basename)) { continue; }

                    cacheEntries[basename].FileName = file;
                }
            }

            resource.StreamEntries = cacheEntries;

            return true;
        }
    }
}
