using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLua;
using LuaL = KeraLua.Lua;

namespace CitizenMP.Server.Resources
{
    class EventScriptFunctions
    {
        [LuaFunction("TriggerEvent")]
        static void TriggerEvent_f(string eventName, params object[] args)
        {
            var serializedArgs = SerializeArguments(args);

            ScriptEnvironment.CurrentEnvironment.Resource.Manager.TriggerEvent(eventName, serializedArgs, -1);
        }

        [LuaFunction("RegisterServerEvent")]
        static void RegisterServerEvent_f(string eventName)
        {
            ScriptEnvironment.CurrentEnvironment.Resource.Manager.GameServer.WhitelistEvent(eventName);
        }

        static string SerializeArguments(object[] args)
        {
            var lua = ScriptEnvironment.CurrentEnvironment.LuaState;
            var L = ScriptEnvironment.CurrentEnvironment.NativeLuaState;

            LuaL.LuaCreateTable(L, args.Length, 0);

            int table = LuaL.LuaGetTop(L);

            for (int i = 0; i < args.Length; i++)
            {
                lua.Push(args[i]);
                LuaL.LuaRawSetI(L, table, i + 1);
            }

            LuaL.LuaPushValue(L, table);
            var tableRef = LuaL.LuaLRef(L, -1001000); // NOTE: THIS MAY DIFFER BETWEEN LUA VERSIONS (5.1 USES -10000; WE USE 5.2 RIGHT NOW)
            var tableNative = new LuaTable(tableRef, lua);

            var packer = (Func<LuaTable, string>)lua.GetFunction(typeof(Func<LuaTable, string>), "msgpack.pack");
            var str = packer(tableNative);

            tableNative.Dispose();

            return str;
        }
    }
}
