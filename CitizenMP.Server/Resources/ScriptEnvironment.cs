using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace CitizenMP.Server.Resources
{
    class ScriptEnvironment : IDisposable
    {
        private Resource m_resource;
        private LuaGlobal m_luaEnvironment;

        private static Lua ms_luaState;
        private static ILuaDebug ms_luaDebug;
        private static LuaCompileOptions ms_luaCompileOptions;
        private static List<KeyValuePair<string, MethodInfo>> ms_luaFunctions = new List<KeyValuePair<string, MethodInfo>>();
        //private static List<KeyValuePair<string, LuaNativeFunction>> ms_nativeFunctions = new List<KeyValuePair<string, LuaNativeFunction>>();

        [ThreadStatic]
        private static ScriptEnvironment ms_currentEnvironment;

        public static ScriptEnvironment CurrentEnvironment
        {
            get
            {
                return ms_currentEnvironment;
            }
        }

        [ThreadStatic]
        private static ScriptEnvironment ms_lastEnvironment;

        [ThreadStatic]
        private static int refCount;

        public static ScriptEnvironment LastEnvironment
        {
            get
            {
                return ms_lastEnvironment;
            }
            private set
            {
                if (ms_lastEnvironment == null && value != null)
                {
                    refCount++;
                }
                else if (ms_lastEnvironment != null && value == null)
                {
                    refCount--;
                }

                ms_lastEnvironment = value;
            }
        }

        public static ScriptEnvironment InvokingEnvironment
        {
            get
            {
                if (CurrentEnvironment.Resource != null && CurrentEnvironment.Resource.State == ResourceState.Parsing)
                {
                    return CurrentEnvironment;
                }

                return (LastEnvironment ?? CurrentEnvironment);
            }
        }

        public Resource Resource
        {
            get
            {
                return m_resource;
            }
        }

        public Lua LuaState
        {
            get
            {
                return ms_luaState;
            }
        }

        public LuaGlobal LuaEnvironment
        {
            get
            {
                return m_luaEnvironment;
            }
        }

        /*public LuaState NativeLuaState
        {
            get
            {
                return m_luaNative;
            }
        }*/

        private static Random ms_instanceGen;

        public uint InstanceID { get; set; }

        static ScriptEnvironment()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var method in methods)
                {
                    var luaAttribute = method.GetCustomAttribute<LuaMemberAttribute>();

                    if (luaAttribute != null)
                    {
                        ms_luaFunctions.Add(new KeyValuePair<string, MethodInfo>(luaAttribute.Name, method));
                    }
                }
            }

            ms_instanceGen = new Random();

            Extensions.Initialize();
        }

        public ScriptEnvironment(Resource resource)
        {
            m_resource = resource;

            InstanceID = (uint)ms_instanceGen.Next();
        }

        private static LuaChunk[] ms_initChunks;

        private List<LuaChunk> m_curChunks = new List<LuaChunk>();

        public bool Create()
        {
            ScriptEnvironment lastEnvironment = null, oldLastEnvironment = null;

            try
            {
                if (ms_luaState == null)
                {
                    ms_luaState = new Lua();

                    //ms_luaDebug = new LuaStackTraceDebugger();
                    ms_luaDebug = null;

                    if (Resource.Manager.Configuration.ScriptDebug)
                    {
                        ms_luaDebug = new LuaStackTraceDebugger();
                    }

                    ms_luaCompileOptions = new LuaCompileOptions();
                    ms_luaCompileOptions.DebugEngine = ms_luaDebug;

                    ms_initChunks = new []
                    {
                        ms_luaState.CompileChunk("system/MessagePack.lua", ms_luaCompileOptions),
                        ms_luaState.CompileChunk("system/dkjson.lua", ms_luaCompileOptions),
                        ms_luaState.CompileChunk("system/resource_init.lua", ms_luaCompileOptions)
                    };
                }

                m_luaEnvironment = ms_luaState.CreateEnvironment();

                foreach (var func in ms_luaFunctions)
                {
                    //m_luaEnvironment[func.Key] = Delegate.CreateDelegate
                    var parameters = func.Value.GetParameters()
                                    .Select(p => p.ParameterType)
                                    .Concat(new Type[] { func.Value.ReturnType })
                                    .ToArray();

                    var delegType = Expression.GetDelegateType
                    (
                        parameters
                    );

                    var deleg = Delegate.CreateDelegate
                    (
                        delegType,
                        null,
                        func.Value
                    );

                    var expParameters = parameters.Take(parameters.Count() - 1).Select(a => Expression.Parameter(a)).ToArray();

                    var pushedEnvironment = Expression.Variable(typeof(PushedEnvironment));

                    Expression<Func<PushedEnvironment>> preFunc = () => PushEnvironment(this);
                    Expression<Action<PushedEnvironment>> postFunc = env => env.PopEnvironment();

                    Expression body;

                    if (func.Value.ReturnType.Name != "Void")
                    {
                        var retval = Expression.Variable(func.Value.ReturnType);

                        body = Expression.Block
                        (
                            func.Value.ReturnType,
                            new[] { retval, pushedEnvironment },

                            Expression.Assign
                            (
                                pushedEnvironment,
                                Expression.Invoke(preFunc)
                            ),

                            Expression.Assign
                            (
                                retval,
                                Expression.Call(func.Value, expParameters)
                            ),

                            Expression.Invoke(postFunc, pushedEnvironment),

                            retval
                        );
                    }
                    else
                    {
                        body = Expression.Block
                        (
                            func.Value.ReturnType,
                            new[] { pushedEnvironment },

                            Expression.Assign
                            (
                                pushedEnvironment,
                                Expression.Invoke(preFunc)
                            ),

                            Expression.Call(func.Value, expParameters),

                            Expression.Invoke(postFunc, pushedEnvironment)
                        );
                    }

                    var lambda = Expression.Lambda(delegType, body, expParameters);

                    m_luaEnvironment[func.Key] = lambda.Compile();
                }

                InitHandler = null;

                /*m_luaNative = LuaL.LuaLNewState();
                LuaL.LuaLOpenLibs(m_luaNative);

                LuaLib.LuaNewTable(m_luaNative);
                LuaLib.LuaSetGlobal(m_luaNative, "luanet");

                InitHandler = null;

                m_luaState = new NLua.Lua(m_luaNative);*/

                lock (m_luaEnvironment)
                {
                    lastEnvironment = ms_currentEnvironment;
                    ms_currentEnvironment = this;

                    oldLastEnvironment = LastEnvironment;
                    LastEnvironment = lastEnvironment;

                    // load global data files
                    foreach (var chunk in ms_initChunks)
                    {
                        m_luaEnvironment.DoChunk(chunk);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error creating script environment for resource " + m_resource.Name + ": " + e.Message, e);

                if (e.InnerException != null)
                {
                    this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                }

                PrintLuaStackTrace(e);
            }
            finally
            {
                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;
            }

            return false;
        }

        public void Dispose()
        {
            if (ms_currentEnvironment == this)
            {
                throw new InvalidOperationException("Tried to dispose the current script environment");
            }

            /*var field = ms_luaState.GetType().GetField("setMemberBinder", BindingFlags.NonPublic | BindingFlags.Instance);
            var binders = (Dictionary<string, System.Runtime.CompilerServices.CallSiteBinder>)field.GetValue(ms_luaState);

            Console.WriteLine("--- BOUNDARY ---");

            foreach (var binder in binders)
            {
                var fields = binder.Value.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                if (fields.Length < 2)
                {
                    continue;
                }

                field = fields[1];
                var cache = (Dictionary<Type, Object>)field.GetValue(binder.Value);

                if (cache == null)
                {
                    continue;
                }

                foreach (var val in cache)
                {
                    field = val.Value.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(a => a.Name == "_rules");

                    if (field != null)
                    {
                        var rules = field.GetValue(val.Value);

                        var prop = rules.GetType().GetProperty("Length");
                        Console.WriteLine("{0}: {1}", binder.Key, prop.GetValue(rules));
                    }
                }
            }*/

            m_curChunks.Clear();

            GC.Collect();
        }

        internal class PushedEnvironment
        {
            public ScriptEnvironment LastEnvironment { get; set; }
            public ScriptEnvironment OldLastEnvironment { get; set; }

            public void PopEnvironment()
            {
                ms_currentEnvironment = LastEnvironment;
                ScriptEnvironment.LastEnvironment = OldLastEnvironment;
            }
        }

        internal static PushedEnvironment PushEnvironment(ScriptEnvironment env)
        {
            var penv = new PushedEnvironment();
            penv.LastEnvironment = ms_currentEnvironment;
            ms_currentEnvironment = env;

            penv.OldLastEnvironment = LastEnvironment;
            LastEnvironment = penv.LastEnvironment;

            return penv;
        }

        public Delegate InitHandler { get; set; }

        public bool LoadScripts()
        {
            ScriptEnvironment lastEnvironment = null, oldLastEnvironment = null;

            try
            {
                lastEnvironment = ms_currentEnvironment;
                ms_currentEnvironment = this;

                oldLastEnvironment = LastEnvironment;
                LastEnvironment = lastEnvironment;

                // load scripts defined in this resource
                foreach (var script in m_resource.ServerScripts)
                {
                    lock (m_luaEnvironment)
                    {
                        var chunk = ms_luaState.CompileChunk(Path.Combine(m_resource.Path, script), ms_luaCompileOptions);
                        m_luaEnvironment.DoChunk(chunk);
                        m_curChunks.Add(chunk);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error creating script environment for resource " + m_resource.Name + ": " + e.Message, e);

                if (e.InnerException != null)
                {
                    this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                }

                PrintLuaStackTrace(e);
            }
            finally
            {
                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;
            }

            return false;
        }

        private List<string> m_serverScripts = new List<string>();

        public void AddServerScript(string script)
        {
            m_serverScripts.Add(script);
        }

        public bool DoInitFile(bool preParse)
        {
            ScriptEnvironment lastEnvironment = null, oldLastEnvironment = null;

            try
            {
                lastEnvironment = ms_currentEnvironment;
                ms_currentEnvironment = this;

                oldLastEnvironment = LastEnvironment;
                LastEnvironment = lastEnvironment;

                lock (m_luaEnvironment)
                {
                    var initFunction = ms_luaState.CompileChunk(Path.Combine(m_resource.Path, "__resource.lua"), ms_luaCompileOptions);
                    var initDelegate = new Func<LuaResult>(() => m_luaEnvironment.DoChunk(initFunction));

                    InitHandler.DynamicInvoke(initDelegate, preParse);
                }

                if (!preParse)
                {
                    foreach (var script in m_serverScripts)
                    {
                        var chunk = ms_luaState.CompileChunk(Path.Combine(m_resource.Path, script), ms_luaCompileOptions);
                        m_luaEnvironment.DoChunk(chunk);
                        m_curChunks.Add(chunk);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error creating script environment for resource " + m_resource.Name + ": " + e.Message, e);

                if (e.InnerException != null)
                {
                    this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                }

                PrintLuaStackTrace(e);
            }
            finally
            {
                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;
            }

            return false;
        }

        public void TriggerEvent(string eventName, string argsSerialized, int source)
        {
            List<Delegate> eventHandlers;

            if (!m_eventHandlers.TryGetValue(eventName, out eventHandlers))
            {
                return;
            }

            m_luaEnvironment["source"] = source;

            var lastEnvironment = ms_currentEnvironment;
            ms_currentEnvironment = this;

            var oldLastEnvironment = LastEnvironment;
            LastEnvironment = lastEnvironment;

            //var unpacker = (Func<object, LuaResult>)((LuaTable)m_luaEnvironment["msgpack"])["unpack"];
            //var table = unpacker(argsSerialized);

            dynamic luaEnvironment = m_luaEnvironment;
            LuaTable table = luaEnvironment.msgpack.unpack(argsSerialized);

            var args = new object[0];

            if (table != null)
            {
                args = new object[table.Length];
                var i = 0;

                foreach (var value in table)
                {
                    args[i] = value.Value;
                    i++;
                }
            }

            foreach (var handler in eventHandlers)
            {
                try
                {
                    var methodParameters = handler.Method.GetParameters();
                    var localArgs = args;
                    int ignoreAppend = 0;

                    if (methodParameters.Length >= 1 && (methodParameters.Last().ParameterType == typeof(LuaTable) || methodParameters.First().ParameterType == typeof(Closure)))
                    {
                        ignoreAppend = 1;
                    }

                    localArgs = localArgs.Take(methodParameters.Length - ignoreAppend).ToArray();

                    handler.DynamicInvoke(localArgs);
                }
                catch (Exception e)
                {
                    Game.RconPrint.Print("Error in resource {0}: {1}\n", m_resource.Name, e.Message);

                    this.Log().Error(() => "Error executing event handler for event " + eventName + " in resource " + m_resource.Name + ": " + e.Message, e);

                    PrintLuaStackTrace(e);

                    while (e != null)
                    {
                        if (e.InnerException != null)
                        {
                            this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);

                            PrintLuaStackTrace(e.InnerException);
                        }

                        e = e.InnerException;
                    }

                    eventHandlers.Clear();

                    ms_currentEnvironment = lastEnvironment;
                    LastEnvironment = oldLastEnvironment;

                    return;
                }
            }

            ms_currentEnvironment = lastEnvironment;
            LastEnvironment = oldLastEnvironment;
        }

        private int m_referenceNum;
        private Dictionary<int, Delegate> m_luaReferences = new Dictionary<int, Delegate>();

        public Delegate GetRef(int reference)
        {
            var func = m_luaReferences[reference];

            return func;
        }

        public string CallExport(Delegate func, string argsSerialized)
        {
            lock (m_luaEnvironment)
            {
                var lastEnvironment = ms_currentEnvironment;
                ms_currentEnvironment = this;

                var oldLastEnvironment = LastEnvironment;
                LastEnvironment = lastEnvironment;

                string retstr = "";

                try
                {
                    // unpack
                    var unpacker = (Func<string, LuaTable>)((LuaTable)m_luaEnvironment["msgpack"])["unpack"];
                    var table = unpacker(argsSerialized);

                    var args = new object[table.Length];
                    var i = 0;

                    foreach (var value in table)
                    {
                        args[i] = value.Value;
                        i++;
                    }

                    // invoke
                    var objects = (LuaResult)func.DynamicInvoke(args.Take(func.Method.GetParameters().Length - 1).ToArray());

                    // pack return values
                    retstr = EventScriptFunctions.SerializeArguments(objects);
                }
                catch (Exception e)
                {
                    this.Log().Error(() => "Error invoking reference for resource " + m_resource.Name + ": " + e.Message, e);

                    if (e.InnerException != null)
                    {
                        this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                    }

                    PrintLuaStackTrace(e);
                }

                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;

                return retstr;
            }
        }

        internal static void PrintLuaStackTrace(Exception e)
        {
            var data = LuaExceptionData.GetData(e);

            if (data != null)
            {
                foreach (var frame in data)
                {
                    e.Log().Error(frame.ToString());
                }
            }
        }

        public int AddRef(Delegate method)
        {
            int refNum = m_referenceNum++;

            m_luaReferences.Add(refNum, method);

            return refNum;
        }

        public bool HasRef(int reference)
        {
            return m_luaReferences.ContainsKey(reference);
        }

        public void RemoveRef(int reference)
        {
            m_luaReferences.Remove(reference);
        }

        class ScriptTimer
        {
            public Delegate Function { get; set; }
            public long TickFrom { get; set; }
        }

        private List<ScriptTimer> m_timers = new List<ScriptTimer>();

        public void Tick()
        {
            var timers = m_timers.GetRange(0, m_timers.Count);
            var now = Time.CurrentTime;

            foreach (var timer in timers)
            {
                if (now >= timer.TickFrom)
                {
                    lock (m_luaEnvironment)
                    {
                        var lastEnvironment = ms_currentEnvironment;
                        ms_currentEnvironment = this;

                        var oldLastEnvironment = LastEnvironment;
                        LastEnvironment = lastEnvironment;

                        try
                        {
                            timer.Function.DynamicInvoke();
                        }
                        catch (Exception e)
                        {
                            this.Log().Error(() => "Error invoking timer in resource " + m_resource.Name + ": " + e.Message, e);

                            PrintLuaStackTrace(e);

                            if (e.InnerException != null)
                            {
                                this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);

                                PrintLuaStackTrace(e.InnerException);
                            }
                        }

                        ms_currentEnvironment = lastEnvironment;
                        LastEnvironment = oldLastEnvironment;

                        m_timers.Remove(timer);
                    }
                }
            }
        }

        public void SetTimeout(int milliseconds, Delegate callback)
        {
            var newSpan = Time.CurrentTime + milliseconds;

            m_timers.Add(new ScriptTimer() { TickFrom = newSpan, Function = callback });
        }

        [LuaMember("SetTimeout")]
        static void SetTimeout_f(int milliseconds, Delegate callback)
        {
            ms_currentEnvironment.SetTimeout(milliseconds, callback);
        }
        
        [LuaMember("AddEventHandler")]
        static void AddEventHandler_f(string eventName, Delegate eventHandler)
        {
            ms_currentEnvironment.AddEventHandler(eventName, eventHandler);
        }

        [LuaMember("GetInstanceId")]
        static int GetInstanceId_f()
        {
            return (int)ms_currentEnvironment.InstanceID;
        }

        private Dictionary<string, List<Delegate>> m_eventHandlers = new Dictionary<string, List<Delegate>>();

        public void AddEventHandler(string eventName, Delegate eventHandler)
        {
            if (!m_eventHandlers.ContainsKey(eventName))
            {
                m_eventHandlers[eventName] = new List<Delegate>();
            }

            m_eventHandlers[eventName].Add(eventHandler);
        }
    }
}
/*
namespace CitizenMP.Server
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    sealed class LuaFunctionAttribute : Attribute
    {
        public LuaFunctionAttribute(string functionName)
        {
            FunctionName = functionName;
        }

        public string FunctionName { get; private set; }
    }
}
*/