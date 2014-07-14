using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using NLua;
using LuaL = KeraLua.Lua;

namespace CitizenMP.Server.Resources
{
    class EventScriptFunctions
    {
        [LuaFunction("CancelEvent")]
        static void CancelEvent_f()
        {
            ScriptEnvironment.CurrentEnvironment.Resource.Manager.CancelEvent();
        }

        [LuaFunction("WasEventCanceled")]
        static bool WasEventCanceled_f()
        {
            return ScriptEnvironment.CurrentEnvironment.Resource.Manager.WasEventCanceled();
        }

        [LuaFunction("TriggerEvent")]
        static bool TriggerEvent_f(string eventName, params object[] args)
        {
            var serializedArgs = SerializeArguments(args);

            return ScriptEnvironment.CurrentEnvironment.Resource.Manager.TriggerEvent(eventName, serializedArgs, -1);
        }

        [LuaFunction("TriggerClientEvent")]
        static void TriggerClientEvent_f(string eventName, int netID, params object[] args)
        {
            var serializedArgs = SerializeArguments(args);

            ScriptEnvironment.CurrentEnvironment.Resource.Manager.GameServer.TriggerClientEvent(eventName, serializedArgs, netID, 65535);
        }

        [LuaFunction("RegisterServerEvent")]
        static void RegisterServerEvent_f(string eventName)
        {
            ScriptEnvironment.CurrentEnvironment.Resource.Manager.GameServer.WhitelistEvent(eventName);
        }

        [LuaFunction("GetFuncRef")]
        static void GetFuncRef_f(LuaFunction func, out int reference, out string resource)
        {
            var lua = ScriptEnvironment.CurrentEnvironment.LuaState;
            var L = ScriptEnvironment.CurrentEnvironment.NativeLuaState;

            lua.Push(func);
            var funcRef = LuaL.LuaLRef(L, -1001000);

            reference = funcRef;
            resource = ScriptEnvironment.CurrentEnvironment.Resource.Name;
        }

        static Resource ValidateResourceAndRef(int reference, string resourceName)
        {
            var resource = ScriptEnvironment.CurrentEnvironment.Resource.Manager.GetResource(resourceName);

            if (resource == null)
            {
                throw new ArgumentException("Invalid resource name.");
            }

            if (resource.State != ResourceState.Running && resource.State != ResourceState.Starting && resource.State != ResourceState.Parsing)
            {
                throw new ArgumentException("Resource wasn't running.");
            }

            // TODO: hasRef

            return resource;
        }

        delegate object CallDelegate(params object[] args);

        [LuaFunction("GetFuncFromRef")]
        static LuaUserData GetFuncFromRef_f(int reference, string resourceName)
        {
            var resource = ValidateResourceAndRef(reference, resourceName);

            var lua = ScriptEnvironment.CurrentEnvironment.LuaState;
            var L = ScriptEnvironment.CurrentEnvironment.NativeLuaState;

            // start a metatable
            LuaL.LuaCreateTable(L, 0, 2);

            int metatable = LuaL.LuaGetTop(L);

            lua.Push("__gc");

            lua.Push(new LuaFunction((l) =>
            {
                var thisResource = ValidateResourceAndRef(reference, resourceName);

                thisResource.RemoveRef(reference);

                return 0;
            }, lua));

            LuaL.LuaSetTable(L, metatable);

            LuaL.LuaPushString(L, "__call");

            LuaL.LuaPushStdCallCFunction(L, (l) =>
            {
                var thisResource = ValidateResourceAndRef(reference, resourceName);

                // serialize
                int nargs = LuaL.LuaGetTop(L);

                var arguments = SerializeArguments(L, 2, nargs - 1);

                // call
                var retval = thisResource.CallRef(reference, arguments);

                // deserialize
                DeserializeArguments(L, retval, out nargs);

                return nargs;
            });

            LuaL.LuaSetTable(L, metatable);

            // create a dummy userdata
            var resourceNameBytes = Encoding.UTF8.GetBytes(resourceName);
                        var bytes = new byte[4 + resourceNameBytes.Length + 2];

            bytes[0] = 1;
            Array.Copy(BitConverter.GetBytes(reference), 0, bytes, 1, 4);
            Array.Copy(resourceNameBytes, 0, bytes, 5, resourceNameBytes.Length);

            var udata = LuaL.LuaNewUserData(L, (uint)(bytes.Length));
            Marshal.Copy(bytes, 0, udata, bytes.Length);

            LuaL.LuaPushValue(L, metatable);
            LuaL.LuaSetMetatable(L, -2);

            LuaL.LuaRemove(L, metatable);

            var udataRef = LuaL.LuaLRef(L, -1001000);
            var udataNative = new LuaUserData(udataRef, ScriptEnvironment.CurrentEnvironment.LuaState);

            return udataNative;
        }

        public static void DeserializeArguments(KeraLua.LuaState L, string arguments, out int numArgs)
        {
            var unpacker = (Func<string, LuaTable>)ScriptEnvironment.CurrentEnvironment.LuaState.GetFunction(typeof(Func<string, LuaTable>), "msgpack.unpack");
            var table = unpacker(arguments);

            if (table != null)
            {
                numArgs = table.Values.Count;

                foreach (var value in table.Values)
                {
                    ScriptEnvironment.CurrentEnvironment.LuaState.Push(value);
                }

                table.Dispose();
            }
            else
            {
                numArgs = 0;
            }
        }

        public static string SerializeArguments(KeraLua.LuaState L, int firstArg, int numArgs)
        {
            LuaL.LuaCreateTable(L, numArgs, 0);

            int table = LuaL.LuaGetTop(L);

            // save arguments in table
            for (int i = firstArg; i < (firstArg + numArgs); i++)
            {
                LuaL.LuaPushValue(L, i);
                LuaL.LuaRawSetI(L, table, (i - firstArg) + 1);
            }

            // call C function
            LuaL.LuaPushValue(L, table);
            var tableRef = LuaL.LuaLRef(L, -1001000); // NOTE: THIS MAY DIFFER BETWEEN LUA VERSIONS (5.1 USES -10000; WE USE 5.2 RIGHT NOW)
            var tableNative = new LuaTable(tableRef, ScriptEnvironment.CurrentEnvironment.LuaState);

            var packer = (Func<LuaTable, string>)ScriptEnvironment.CurrentEnvironment.LuaState.GetFunction(typeof(Func<LuaTable, string>), "msgpack.pack");
            var str = packer(tableNative);

            tableNative.Dispose();

            return str;
        }

        public static string SerializeArguments(object[] args)
        {
            if (args == null)
            {
                return "\xC0";
            }

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
