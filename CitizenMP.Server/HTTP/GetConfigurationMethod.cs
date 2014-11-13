using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using uhttpsharp;
using uhttpsharp.Headers;
using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.HTTP
{
    static class GetConfigurationMethod
    {
        public static Func<IHttpHeaders, IHttpContext, Task<JObject>> Get(Configuration config, Resources.ResourceManager resourceMgr)
        {
            return (headers, context) =>
            {
                var result = new JObject();
                var resources = new JArray();

                var resourceSource = resourceMgr.GetRunningResources();
                string resourceFilter;

                if (headers.TryGetByName("resources", out resourceFilter))
                {
                    var resourceNames = resourceFilter.Split(';');

                    resourceSource = resourceSource.Where(r => resourceNames.Contains(r.Name));
                }

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

                    var configEntry = resourceMgr.Configuration.GetDownloadConfiguration(resource.Name);

                    if (configEntry != null)
                    {
                        if (!string.IsNullOrWhiteSpace(configEntry.BaseURL))
                        {
                            rObject["fileServer"] = configEntry.BaseURL;
                        }
                    }

                    resources.Add(rObject);
                }

                // add the imports, if any
                if (config.Imports != null)
                {
                    var imports = new JArray();

                    config.Imports.ForEach(a => imports.Add(a.ConfigURL));

                    result["imports"] = imports;
                }

                result["resources"] = resources;
                result["fileServer"] = "http://%s/files/";

                result["loadScreen"] = "nui://keks/index.html";

                var source = new TaskCompletionSource<JObject>();
                source.SetResult(result);

                return source.Task;
            };
        }
    }
}
