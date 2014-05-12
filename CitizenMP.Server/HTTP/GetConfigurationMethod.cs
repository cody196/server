using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using uhttpsharp.Headers;
using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.HTTP
{
    static class GetConfigurationMethod
    {
        public static Func<IHttpHeaders, JObject> Get(Resources.ResourceManager resourceMgr)
        {
            return (headers) =>
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

                foreach (var resource in resourceMgr.GetRunningResources())
                {
                    var files = new JObject();
                    files["resource.rpf"] = resource.ClientPackageHash;

                    var rObject = new JObject();
                    rObject["name"] = resource.Name;
                    rObject["files"] = files;

                    resources.Add(rObject);
                }

                result["resources"] = resources;
                result["fileServer"] = "http://%s/files/";

                return result;
            };
        }
    }
}
