using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace CitizenWorld.DAL
{
    public class DataManager
    {
        public static string Namespace { get; set; }

        static DataManager()
        {
            Namespace = ScriptEnvironment.ResourceName;
        }
    }
}
