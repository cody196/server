using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neo.IronLua;

namespace CitizenMP.Server.Resources
{
    class LogScriptFunctions
    {
        [LuaMember("print")]
        static void Print_f(params object[] arguments)
        {
            ScriptEnvironment.CurrentEnvironment.Log("script print").Info(() =>
            {
                return string.Join(" ", arguments.Select(a => a ?? "null").Select(a => a.ToString()));
            });
        }
    }
}
