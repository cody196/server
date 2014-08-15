using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using KeraLua;
using NLua;

using LuaL = KeraLua.Lua;

namespace CitizenMP.Server.Resources
{
    class ScriptEnvironment : IDisposable
    {
        private Resource m_resource;
        private LuaState m_luaNative;
        private NLua.Lua m_luaState;

        private static List<KeyValuePair<string, MethodInfo>> ms_luaFunctions = new List<KeyValuePair<string, MethodInfo>>();

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

        public NLua.Lua LuaState
        {
            get
            {
                return m_luaState;
            }
        }

        public LuaState NativeLuaState
        {
            get
            {
                return m_luaNative;
            }
        }

        static ScriptEnvironment()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var method in methods)
                {
                    var luaAttribute = method.GetCustomAttribute<LuaFunctionAttribute>();

                    if (luaAttribute != null)
                    {
                        ms_luaFunctions.Add(new KeyValuePair<string, MethodInfo>(luaAttribute.FunctionName, method));
                    }
                }
            }
        }

        public ScriptEnvironment(Resource resource)
        {
            m_resource = resource;
        }

        public bool Create()
        {
            ScriptEnvironment lastEnvironment = null, oldLastEnvironment = null;

            try
            {
                m_luaNative = LuaL.LuaLNewState();
                LuaL.LuaLOpenLibs(m_luaNative);

                LuaLib.LuaNewTable(m_luaNative);
                LuaLib.LuaSetGlobal(m_luaNative, "luanet");

                m_luaState = new NLua.Lua(m_luaNative);

                lastEnvironment = ms_currentEnvironment;
                ms_currentEnvironment = this;

                oldLastEnvironment = LastEnvironment;
                LastEnvironment = lastEnvironment;

                // register our global functions
                foreach (var func in ms_luaFunctions)
                {
                    m_luaState.RegisterFunction(func.Key, func.Value);
                }

                // load global data files
                m_luaState.DoFile("system/resource_init.lua");
                m_luaState.DoFile("system/MessagePack.lua");
                m_luaState.DoFile("system/dkjson.lua");

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error creating script environment for resource " + m_resource.Name + ": " + e.Message, e);

                if (e.InnerException != null)
                {
                    this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                }
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

            m_luaState.Close();
            m_luaState.Dispose();
        }

        public LuaFunction InitHandler { get; set; }

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
                    m_luaState.DoFile(Path.Combine(m_resource.Path, script));
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
            }
            finally
            {
                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;
            }

            return false;
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

                var initFunction = m_luaState.LoadFile(Path.Combine(m_resource.Path, "__resource.lua"));

                InitHandler.Call(initFunction, preParse);

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error creating script environment for resource " + m_resource.Name + ": " + e.Message, e);

                if (e.InnerException != null)
                {
                    this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                }
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
            List<LuaFunction> eventHandlers;

            if (!m_eventHandlers.TryGetValue(eventName, out eventHandlers))
            {
                return;
            }

            lock (m_luaState)
            {
                var L = m_luaNative;
                LuaL.LuaPushNumber(L, source);
                LuaL.LuaNetSetGlobal(L, "source");

                var lastEnvironment = ms_currentEnvironment;
                ms_currentEnvironment = this;

                var oldLastEnvironment = LastEnvironment;
                LastEnvironment = lastEnvironment;

                var unpacker = (Func<string, LuaTable>)m_luaState.GetFunction(typeof(Func<string, LuaTable>), "msgpack.unpack");
                var table = unpacker(argsSerialized);

                var args = new object[table.Values.Count];
                var i = 0;

                foreach (var value in table.Values)
                {
                    args[i] = value;
                    i++;
                }

                foreach (var handler in eventHandlers)
                {
                    try
                    {
                        handler.Call(args);
                    }
                    catch (NLua.Exceptions.LuaException e)
                    {
                        this.Log().Error(() => "Error executing event handler for event " + eventName + " in resource " + m_resource.Name + ": " + e.Message, e);

                        if (e.InnerException != null)
                        {
                            this.Log().Error(() => "Inner exception: " + e.InnerException.Message, e.InnerException);
                        }

                        Game.RconPrint.Print("Error in resource {0}: {1}\n", m_resource.Name, e.Message);

                        eventHandlers.Clear();

                        ms_currentEnvironment = lastEnvironment;
                        LastEnvironment = oldLastEnvironment;

                        return;
                    }
                }

                table.Dispose();

                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;
            }
        }

        public string CallExport(int luaRef, string argsSerialized)
        {
            lock (m_luaState)
            {
                var func = new LuaFunction(luaRef, m_luaState);

                var lastEnvironment = ms_currentEnvironment;
                ms_currentEnvironment = this;

                var oldLastEnvironment = LastEnvironment;
                LastEnvironment = lastEnvironment;

                // unpack
                var unpacker = (Func<string, LuaTable>)m_luaState.GetFunction(typeof(Func<string, LuaTable>), "msgpack.unpack");
                var table = unpacker(argsSerialized);

                // make array
                var args = new object[table.Values.Count];
                var i = 0;

                foreach (var value in table.Values)
                {
                    args[i] = value;
                    i++;
                }

                // invoke
                var objects = func.Call(args);
                table.Dispose();

                // pack return values
                var retstr = EventScriptFunctions.SerializeArguments(objects);

                ms_currentEnvironment = lastEnvironment;
                LastEnvironment = oldLastEnvironment;

                return retstr;
            }
        }

        public void RemoveRef(int reference)
        {
            lock (m_luaState)
            {
                LuaL.LuaLUnref(m_luaNative, -1001000, reference);
            }
        }

        class ScriptTimer
        {
            public LuaFunction Function { get; set; }
            public DateTime TickFrom { get; set; }
        }

        private List<ScriptTimer> m_timers = new List<ScriptTimer>();

        public void Tick()
        {
            var timers = m_timers.GetRange(0, m_timers.Count);
            var now = DateTime.UtcNow;

            foreach (var timer in timers)
            {
                if (now >= timer.TickFrom)
                {
                    lock (m_luaState)
                    {
                        var lastEnvironment = ms_currentEnvironment;
                        ms_currentEnvironment = this;

                        var oldLastEnvironment = LastEnvironment;
                        LastEnvironment = lastEnvironment;

                        timer.Function.Call();

                        ms_currentEnvironment = lastEnvironment;
                        LastEnvironment = oldLastEnvironment;

                        m_timers.Remove(timer);
                    }
                }
            }
        }

        public void SetTimeout(int milliseconds, LuaFunction callback)
        {
            var newSpan = DateTime.UtcNow + TimeSpan.FromMilliseconds(milliseconds);

            m_timers.Add(new ScriptTimer() { TickFrom = newSpan, Function = callback });
        }

        [LuaFunction("SetTimeout")]
        static void SetTimeout_f(int milliseconds, LuaFunction callback)
        {
            ms_currentEnvironment.SetTimeout(milliseconds, callback);
        }
        
        [LuaFunction("AddEventHandler")]
        static void AddEventHandler_f(string eventName, LuaFunction eventHandler)
        {
            ms_currentEnvironment.AddEventHandler(eventName, eventHandler);
        }

        private Dictionary<string, List<LuaFunction>> m_eventHandlers = new Dictionary<string, List<LuaFunction>>();

        public void AddEventHandler(string eventName, LuaFunction eventHandler)
        {
            if (!m_eventHandlers.ContainsKey(eventName))
            {
                m_eventHandlers[eventName] = new List<LuaFunction>();
            }

            m_eventHandlers[eventName].Add(eventHandler);
        }
    }
}

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