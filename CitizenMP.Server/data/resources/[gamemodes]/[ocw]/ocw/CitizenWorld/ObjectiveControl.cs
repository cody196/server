using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

using System.Reflection;

namespace CitizenWorld
{
    abstract class Objective : BaseScript
    {
        private VariableSpace m_variableSpace;

        protected VariableSpace VariableSpace
        {
            get
            {
                return m_variableSpace;
            }
        }

        private EntityManager m_entityManager;

        protected EntityManager Entities
        {
            get
            {
                return m_entityManager;
            }
        }

        public int ObjectiveId
        {
            get;
            set;
        }

        public Objective(object data)
        {
            Tick += Objective_Tick;
        }

        async Task Objective_Tick()
        {
            await Entities.Tick();
        }

        public virtual Task Initialize()
        {
            return Task.FromResult(false);
        }

        internal void SetVariableSpace(VariableSpace space)
        {
            m_variableSpace = space;
            m_entityManager = new EntityManager(space);
        }

        private bool m_completed;

        public void CompleteObjective()
        {
            if (!m_completed)
            {
                TriggerServerEvent("ocw:objective:complete", ObjectiveId, m_variableSpace.SpaceId);

                m_completed = true;
            }
        }

        public virtual async Task Defuse()
        {
            Tick -= Objective_Tick;
        }

        private bool m_defused;

        public async Task TryDefuse()
        {
            if (!m_defused)
            {
                Entities.SkipNullHandles = true;

                await Defuse();

                m_defused = true;
            }
        }
    }

    public class ObjectiveControl : BaseScript
    {
        private VariableSpace m_objectiveSpace;

        private Dictionary<int, Objective> m_instances = new Dictionary<int, Objective>();

        public ObjectiveControl()
        {
            EventHandlers["ocw:regObjSpace"] += new Action<int>(objSpace =>
            {
                m_objectiveSpace = VariableSpaceDispatcher.GetVariableSpace(objSpace);

                m_objectiveSpace.RegisterSetHandler("objective:", async (key, value) =>
                {
                    if (value.GetType() != typeof(bool))
                    {
                        Debug.WriteLine("created objective {0}", value.typeId);

                        TriggerEvent("ocw:newObjective", int.Parse(key.Split(':')[1]), value.typeId, value);
                    }
                    else
                    {
                        Objective instance;

                        if (m_instances.TryGetValue(int.Parse(key.Split(':')[1]), out instance))
                        {
                            await instance.TryDefuse();
                        }
                    }
                });
            });

            EventHandlers["ocw:newObjective"] += new Action<int, string, dynamic>(async (objectiveId, objectiveType, data) =>
            {
                var typeId = Assembly.GetExecutingAssembly().GetTypes().Where(a =>
                {
                    Debug.WriteLine("testing {0} {1}", a.Name, objectiveType);

                    return a.Name.Equals(objectiveType, StringComparison.InvariantCultureIgnoreCase);
                }).Where(a => a.IsSubclassOf(typeof(Objective))).FirstOrDefault();

                if (typeId != null)
                {
                    Debug.WriteLine("Objective {0} resolved to {1}.", objectiveType, typeId.FullName);

                    var instance = Activator.CreateInstance(typeId, data) as Objective;
                    instance.ObjectiveId = objectiveId;

                    if (instance != null)
                    {
                        instance.SetVariableSpace(VariableSpaceDispatcher.GetVariableSpace(data.spaceId));

                        await instance.Initialize();

                        BaseScript.RegisterScript(instance);

                        m_instances[objectiveId] = instance;
                    }
                }
                else
                {
                    Debug.WriteLine("couldn't resolve {0}", typeId);
                }
            });
        }
    }

    class TestObjective : Objective
    {
        public TestObjective(dynamic data)
            : base((object)data)
        {
        }

        private Blip m_blip;

        public override async Task Initialize()
        {
            Debug.WriteLine("Trying to get that ped!");

            var ped = await Entities.GetOrCreate<Ped>("test", "ig_dwayne", new Vector3(21.001f, -40.001f, 15.001f), 0.0f);

            Entities.SetControlTask(ped, async ped_ =>
            {
                if (!ped_.IsAlive)
                {
                    Debug.WriteLine("ped dead, die objective");
                    
                    CompleteObjective();

                    await TryDefuse();
                }
            });

            m_blip = Blip.AddBlip(ped);
            m_blip.Friendly = true;

            ped.Health = 400;

            Entities.SetPedTask(ped, 117, Natives.TASK_GUARD_CURRENT_POSITION, 15.0f, 10.0f, 99999999);
        }

        public override async Task Defuse()
        {
            //Tick -= TestObjective_Tick;

            Debug.WriteLine("DEFUSE");

            await base.Defuse();

            Debug.WriteLine("DEFUSE 2");

            var ped = await Entities.GetOrCreate<Ped>("test", "ig_dwayne", new Vector3(21.001f, -40.001f, 15.001f), 0.0f);

            Debug.WriteLine("DEFUSE 3");

            if (ped != null)
            {
                Debug.WriteLine("DEFUSE 4");

                ped.NoLongerNeeded();
            }

            Debug.WriteLine("DEFUSE 5");

            m_blip.Remove();
        }
    }
}
