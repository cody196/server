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
        public static Func<IHttpHeaders, JObject> Get()
        {
            return (headers) =>
            {
                var result = new JObject();

                var files = new JObject();
                files["resource.rpf"] = "3FF30849558B431E4DA093BA10D62BFACDBC9ED2";

                var lovely = new JObject();
                lovely["name"] = "lovely";
                lovely["files"] = files;

                var resources = new JArray();
                resources.Add(lovely);

                result["resources"] = resources;
                result["fileServer"] = "http://refint.org/files/";

                return result;
            };
        }
    }
}
