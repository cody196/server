using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neo.IronLua;

namespace CitizenMP.Server.Resources
{
    class VMInitScriptFunctions
    {
        [LuaMember("SetResourceInfo")]
        public static void SetResourceInfo_f(string key, string value)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.Info[key] = value;
        }

        [LuaMember("AddClientScript")]
        public static void AddClientScript_f(string script)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.Scripts.Add(script);
        }

        [LuaMember("AddServerScript")]
        public static void AddServerScript_f(string script)
        {
            ScriptEnvironment.CurrentEnvironment.AddServerScript(script);
        }

        [LuaMember("AddAuxFile")]
        public static void AddAuxFile_f(string file)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.AuxFiles.Add(file);
        }

        [LuaMember("AddResourceDependency")]
        public static void AddResourceDependency_f(string resource)
        {
            ScriptEnvironment.InvokingEnvironment.Resource.Dependencies.Add(resource);
        }

        [LuaMember("RegisterInitHandler")]
        public static void RegisterInitHandler_f(Delegate function)
        {
            ScriptEnvironment.CurrentEnvironment.InitHandler = function;
        }
    }
}
