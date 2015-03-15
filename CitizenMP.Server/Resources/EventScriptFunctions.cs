using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Neo.IronLua;

namespace CitizenMP.Server.Resources
{
    class EventScriptFunctions
    {
        [LuaMember("CancelEvent")]
        static void CancelEvent_f()
        {
            ScriptEnvironment.CurrentEnvironment.Resource.Manager.CancelEvent();
        }

        [LuaMember("WasEventCanceled")]
        static bool WasEventCanceled_f()
        {
            return ScriptEnvironment.CurrentEnvironment.Resource.Manager.WasEventCanceled();
        }

        [LuaMember("TriggerEvent")]
        static bool TriggerEvent_f(string eventName, params object[] args)
        {
            var serializedArgs = SerializeArguments(args);

            return ScriptEnvironment.CurrentEnvironment.Resource.Manager.TriggerEvent(eventName, serializedArgs, -1);
        }

        [LuaMember("TriggerClientEvent")]
        static void TriggerClientEvent_f(string eventName, int netID, params object[] args)
        {
            var serializedArgs = SerializeArguments(args);

            ScriptEnvironment.CurrentEnvironment.Resource.Manager.GameServer.TriggerClientEvent(eventName, serializedArgs, netID, 65535);
        }

        [LuaMember("RegisterServerEvent")]
        static void RegisterServerEvent_f(string eventName)
        {
            ScriptEnvironment.CurrentEnvironment.Resource.Manager.GameServer.WhitelistEvent(eventName);
        }

        [LuaMember("GetFuncRef")]
        static void GetFuncRef_f(Delegate func, out int reference, out int instance, out string resource)
        {
            reference = ScriptEnvironment.CurrentEnvironment.AddRef(func);
            instance = (int)ScriptEnvironment.CurrentEnvironment.InstanceID;
            resource = ScriptEnvironment.CurrentEnvironment.Resource.Name;
        }

        static Resource ValidateResourceAndRef(int reference, uint instance, string resourceName)
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

            if (!resource.HasRef(reference, instance))
            {
                return null;
            }

            return resource;
        }

        delegate object CallDelegate(params object[] args);

        [LuaMember("GetFuncFromRef")]
        static LuaTable GetFuncFromRef_f(int reference, uint instance, string resourceName)
        {
            var resource = ValidateResourceAndRef(reference, instance, resourceName);

            var metaTable = new LuaTable();
            var func = resource.GetRef(reference);

            metaTable["__call"] = (CallDelegate)delegate(object[] args)
            {
                var objResource = ValidateResourceAndRef(reference, instance, resourceName);

                if (objResource == null)
                {
                    return null;
                }

                var methodParameters = func.Method.GetParameters();
                var localArgs = args.Skip(1);

                if (methodParameters.Length >= 1 && methodParameters.Last().ParameterType == typeof(LuaTable))
                {
                    localArgs = localArgs.Take(methodParameters.Length - 1);
                }

                return func.DynamicInvoke(localArgs.ToArray());
            };

            var table = new LuaTable();
            table["__reference"] = reference;
            table["__instance"] = instance;
            table["__resource"] = resourceName;

            table.MetaTable = metaTable;

            return table;
        }

        public static string SerializeArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "\xC0";
            }

            var table = new LuaTable();

            for (int i = 1; i <= args.Length; i++)
            {
                table[i] = args[i - 1];
            }

            /*dynamic luaEnvironment = ScriptEnvironment.CurrentEnvironment.LuaEnvironment;
            dynamic msgpack = luaEnvironment.msgpack;
            dynamic pack = msgpack.pack;

            string str = pack(table);*/

            var luaEnvironment = ScriptEnvironment.CurrentEnvironment.LuaEnvironment;
            var method = (Func<object, LuaResult>)((LuaTable)luaEnvironment["msgpack"])["pack"];

            return method(table).ToString();
        }

        public static string SerializeArguments(LuaResult args)
        {
            if (args == null)
            {
                return "\xC0";
            }

            var table = new LuaTable();
            
            for (int i = 0; i < args.Count; i++)
            {
                table[i] = args[i];
            }

            var luaEnvironment = ScriptEnvironment.CurrentEnvironment.LuaEnvironment;
            var packer = (Func<LuaTable, string>)((LuaTable)luaEnvironment["msgpack"])["pack"];

            var str = packer(table);

            return str;
        }

        // required for msgpack
        [LuaMember("ldexp")]
        public static double ldexp(double x, int exp)
        {
            return x * Math.Pow(2, exp);
        }

        [LuaMember("frexp")]
        public static void frexp(double x, out double fr, out int exp)
        {
            exp = (int)Math.Floor(Math.Log(x) / Math.Log(2)) + 1;
            fr = 1 - (Math.Pow(2, exp) - x) / Math.Pow(2, exp);
        }
    }
}
