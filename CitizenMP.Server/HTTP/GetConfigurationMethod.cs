using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using uhttpsharp;
using uhttpsharp.Headers;
using Newtonsoft.Json.Linq;

using CitizenMP.Server.Resources;

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
                else
                {
                    // add the imports, if any
                    if (config.Imports != null)
                    {
                        var imports = new JArray();

                        config.Imports.ForEach(a => imports.Add(a.ConfigURL));

                        result["imports"] = imports;
                    }
                }

                // generate configuration with our filter function
                resourceSource.GenerateConfiguration(resources, (resource, rObject) =>
                {
                    // get download configuration for this resource
                    var configEntry = resourceMgr.Configuration.GetDownloadConfiguration(resource.Name);

                    if (configEntry != null)
                    {
                        if (!string.IsNullOrWhiteSpace(configEntry.BaseURL))
                        {
                            rObject["fileServer"] = configEntry.BaseURL;
                        }
                    }
                });

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
