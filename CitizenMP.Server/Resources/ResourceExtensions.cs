using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.Resources
{
    public static class ResourceExtensions
    {
        public static void GenerateConfiguration(this IEnumerable<Resource> resourceSource, JArray array, Action<Resource, JObject> filter = null)
        {
            foreach (var resource in resourceSource)
            {
                var files = new JObject();
                files["resource.rpf"] = resource.ClientPackageHash;

                var streamFiles = new JObject();

                foreach (var entry in resource.StreamEntries)
                {
                    var obj = new JObject();
                    obj["hash"] = entry.Value.HashString;
                    obj["rscFlags"] = entry.Value.RscFlags;
                    obj["rscVersion"] = entry.Value.RscVersion;
                    obj["size"] = entry.Value.Size;

                    streamFiles[entry.Value.BaseName] = obj;
                }

                var rObject = new JObject();
                rObject["name"] = resource.Name;
                rObject["files"] = files;
                rObject["streamFiles"] = streamFiles;

                // allow filtering
                if (filter != null)
                {
                    filter(resource, rObject);
                }

                array.Add(rObject);
            }
        }
    }
}
