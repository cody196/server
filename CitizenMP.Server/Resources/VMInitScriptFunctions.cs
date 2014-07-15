using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLua;
using LuaL = KeraLua.Lua;

namespace CitizenMP.Server.Resources
{
    class VMInitScriptFunctions
    {
        [LuaFunction("SetResourceInfo")]
        public static void SetResourceInfo_f(string key, string value)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.Info[key] = value;
        }

        [LuaFunction("AddClientScript")]
        public static void AddClientScript_f(string script)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.Scripts.Add(script);
        }

        [LuaFunction("AddServerScript")]
        public static void AddServerScript_f(string script)
        {
            var env = ScriptEnvironment.CurrentEnvironment;
            env.LuaState.DoFile(Path.Combine(env.Resource.Path, script));
        }

        [LuaFunction("AddAuxFile")]
        public static void AddAuxFile_f(string file)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.AuxFiles.Add(file);
        }

        [LuaFunction("AddResourceDependency")]
        public static void AddResourceDependency_f(string resource)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.Dependencies.Add(resource);
        }

        [LuaFunction("RegisterInitHandler")]
        public static void RegisterInitHandler_f(LuaFunction function)
        {
            ScriptEnvironment.CurrentEnvironment.InitHandler = function;
        }
    }
}
