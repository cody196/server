using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace CitizenMP.Server
{
    public class Configuration
    {
        public static Configuration Load(string filename)
        {
            var buffer = File.ReadAllText(filename);

            var deserializer = new Deserializer(ignoreUnmatched: true);
            return deserializer.Deserialize<Configuration>(new StringReader(buffer));
        }

        public List<string> AutoStartResources { get; set; }

        public string RconPassword { get; set; }

        public int ListenPort { get; set; }
    }
}
