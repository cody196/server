using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenMP.Server.Game;

namespace CitizenMP.Server.Resources
{
    class RconScriptFunctions
    {
        [LuaFunction("RconPrint")]
        static void RconPrint_f(string str)
        {
            RconPrint.Print("{0}", str);
        }
    }
}
