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
            try
            {
                m_luaNative = LuaL.LuaLNewState();
                LuaL.LuaLOpenLibs(m_luaNative);

                LuaLib.LuaNewTable(m_luaNative);
                LuaLib.LuaSetGlobal(m_luaNative, "luanet");

                m_luaState = new NLua.Lua(m_luaNative);

                ms_currentEnvironment = this;

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
            }
            finally
            {
                ms_currentEnvironment = null;
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
            try
            {
                ms_currentEnvironment = this;

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
            }
            finally
            {
                ms_currentEnvironment = null;
            }

            return false;
        }

        public bool DoInitFile(bool preParse)
        {
            try
            {
                ms_currentEnvironment = this;

                var initFunction = m_luaState.LoadFile(Path.Combine(m_resource.Path, "__resource.lua"));

                InitHandler.Call(initFunction, preParse);

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error creating script environment for resource " + m_resource.Name + ": " + e.Message, e);
            }
            finally
            {
                ms_currentEnvironment = null;
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

                var unpacker = (Func<string, LuaTable>)m_luaState.GetFunction(typeof(Func<string, LuaTable>), "msgpack.unpack");
                var table = unpacker(argsSerialized);

                var args = new object[table.Values.Count];
                var i = 0;

                foreach (var value in table.Values)
                {
                    args[i] = value;
                    i++;
                }

                ms_currentEnvironment = this;

                foreach (var handler in eventHandlers)
                {
                    try
                    {
                        handler.Call(args);
                    }
                    catch (NLua.Exceptions.LuaException e)
                    {
                        this.Log().Error(() => "Error executing event handler for event " + eventName + " in resource " + m_resource.Name + ": " + e.Message, e);

                        Game.RconPrint.Print("Error in resource {0}: {1}\n", m_resource.Name, e.Message);

                        eventHandlers.Clear();
                        return;
                    }
                }

                table.Dispose();

                ms_currentEnvironment = null;
            }
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