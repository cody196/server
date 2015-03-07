using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace CitizenWorld
{
    class VariableSpace
    {
        private Dictionary<string, dynamic> m_variables = new Dictionary<string, dynamic>();

        private List<Tuple<string, Func<string, dynamic, Task>>> m_setHandlers = new List<Tuple<string, Func<string, dynamic, Task>>>();

        private List<Tuple<string, Func<string, dynamic, Task>>> m_newSetHandlers = new List<Tuple<string, Func<string, dynamic, Task>>>();

        private int m_spaceId;

        public VariableSpace(int spaceId)
        {
            m_spaceId = spaceId;
        }

        public int SpaceId
        {
            get
            {
                return m_spaceId;
            }
        }

        public dynamic this[string key]
        {
            get
            {
                dynamic val;

                if (m_variables.TryGetValue(key, out val))
                {
                    return val;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                SyncValue(key, value);
            }
        }

        public async Task SetValueNoSync(string key, dynamic value)
        {
            m_variables[key] = value;

            foreach (var handler in m_setHandlers)
            {
                if (key.StartsWith(handler.Item1))
                {
                    try
                    {
                        await handler.Item2(key, value);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                    }
                }
            }
        }

        private void SyncValue(string key, dynamic value)
        {
            BaseScript.TriggerServerEvent("ocw:varSpace:reqSet", m_spaceId, key, value);
        }

        public void RegisterSetHandler(string prefix, Func<string, dynamic, Task> setHandler)
        {
            m_setHandlers.Add(Tuple.Create(prefix, setHandler));
            m_newSetHandlers.Add(Tuple.Create(prefix, setHandler));
        }

        public async Task Tick()
        {
            foreach (var setHandler in m_newSetHandlers)
            {
                foreach (var variable in m_variables)
                {
                    if (variable.Key.StartsWith(setHandler.Item1))
                    {
                        try
                        {
                            await setHandler.Item2(variable.Key, variable.Value);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.ToString());
                        }
                    }
                }
            }

            m_newSetHandlers.Clear();
        }
    }

    public class VariableSpaceDispatcher : BaseScript
    {
        private static Dictionary<int, VariableSpace> ms_variableSpaces = new Dictionary<int, VariableSpace>();

        internal static VariableSpace GetVariableSpace(int spaceId)
        {
            VariableSpace space;

            if (ms_variableSpaces.TryGetValue(spaceId, out space))
            {
                return space;
            }
            else
            {
                return null;
            }
        }

        public VariableSpaceDispatcher()
        {
            EventHandlers["ocw:varSpace:create"] += new Action<int, dynamic>(async (space, dataMap) =>
            {
                var varSpace = new VariableSpace(space);
                ms_variableSpaces[space] = varSpace;

                var dictionary = dataMap as IDictionary<string, object>;

                Debug.WriteLine("variable space {0} created (map is {1}, dict is {2})", space, dataMap.GetType().Name, dictionary);

                if (dictionary != null)
                {
                    foreach (var kvp in dictionary)
                    {
                        Debug.WriteLine("setting {0} in space {1} to {2}", kvp.Key, space,kvp.Value);

                        await varSpace.SetValueNoSync(kvp.Key, kvp.Value);
                    }
                }
            });

            EventHandlers["ocw:varSpace:set"] += new Action<int, string, dynamic>((space, key, value) =>
            {
                // get the variable space requested
                VariableSpace varSpace;
                
                if (ms_variableSpaces.TryGetValue(space, out varSpace))
                {
                    Debug.WriteLine("setting {0} in space {1} to {2}", key, space, value);

                    varSpace.SetValueNoSync(key, value);
                }
            });

            EventHandlers["onClientGameTypeStart"] += new Action<string>(a =>
            {
                TriggerServerEvent("ocw:varSpace:resync");
            });

            Tick += VariableSpaceDispatcher_Tick;
        }

        async Task VariableSpaceDispatcher_Tick()
        {
            foreach (var space in ms_variableSpaces)
            {
                await space.Value.Tick();
            }
        }
    }
}
