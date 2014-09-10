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
        public static Func<IHttpHeaders, IHttpContext, Task<JObject>> Get(Resources.ResourceManager resourceMgr)
        {
            return (headers, context) =>
            {
                var result = new JObject();

                /*var files = new JObject();
                files["resource.rpf"] = "4B5511AA0F088F4C98C8BB56932DEF90D80E76C2";

                var lovely = new JObject();
                lovely["name"] = "lovely";
                lovely["files"] = files;

                var resources = new JArray();
                resources.Add(lovely);*/

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

                result["resources"] = resources;
                result["fileServer"] = "http://%s/files/";

                //result["loadScreen"] = "http://www.google.com/";
                result["loadScreen"] = "nui://keks/index.html";

                var source = new TaskCompletionSource<JObject>();
                source.SetResult(result);

                return source.Task;
            };
        }
    }
}
