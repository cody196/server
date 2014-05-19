using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLua;
using LuaL = KeraLua.Lua;

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

        [LuaFunction("RconLog")]
        static void RconLog_f(LuaTable table)
        {
            var packer = (Func<LuaTable, string>)ScriptEnvironment.CurrentEnvironment.LuaState.GetFunction(typeof(Func<LuaTable, string>), "json.encode");
            var str = packer(table);

            var rconLog = ScriptEnvironment.CurrentEnvironment.Resource.Manager.RconLog;

            if (rconLog != null)
            {
                rconLog.Append(str);
            }
        }
    }
}
