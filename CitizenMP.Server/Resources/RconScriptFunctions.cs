using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neo.IronLua;

using CitizenMP.Server.Game;

namespace CitizenMP.Server.Resources
{
    class RconScriptFunctions
    {
        [LuaMember("RconPrint")]
        static void RconPrint_f(string str)
        {
            RconPrint.Print("{0}", str);
        }

        [LuaMember("RconLog")]
        static void RconLog_f(LuaTable table)
        {
            var luaEnvironment = ScriptEnvironment.CurrentEnvironment.LuaEnvironment;
            var packer = (Func<object, object, LuaResult>)((LuaTable)luaEnvironment["json"])["encode"];

            var str = packer(table, null);

            var rconLog = ScriptEnvironment.CurrentEnvironment.Resource.Manager.RconLog;

            if (rconLog != null)
            {
                rconLog.Append(str.Values[0].ToString());
            }
        }
    }
}
