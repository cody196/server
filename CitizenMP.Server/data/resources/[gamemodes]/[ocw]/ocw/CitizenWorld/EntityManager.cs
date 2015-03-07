using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace CitizenWorld
{
    class EntityManager
    {
        private VariableSpace m_variableSpace;

        private HashSet<string> m_creatingPurposes = new HashSet<string>();
        
        public EntityManager(VariableSpace variableSpace)
        {
            m_variableSpace = variableSpace;
        }

        public bool SkipNullHandles { get; set; }

        public async Task<T> GetOrCreate<T>(string purpose, Model model, Vector3 coords, float heading) where T : Entity, new()
        {
            var key = string.Format("em:{0}:{1}", typeof(T).Name, purpose);
            dynamic netId;

            T retEntity;

            Debug.WriteLine("getting the netid from space {0}, we hope...", m_variableSpace.SpaceId);

            while ((netId = m_variableSpace[key]) == null)
            {
                await BaseScript.Delay(0);

                if (Function.Call<bool>(Natives.IS_THIS_MACHINE_THE_SERVER))
                {
                    if (!m_creatingPurposes.Contains(purpose))
                    {
                        m_creatingPurposes.Add(purpose);

                        retEntity = await CreateEntity<T>(model, coords, heading);

                        // an attempt to get a more reliable network ID
                        await BaseScript.Delay(0);

                        netId = GetNetworkId<T>(retEntity);

                        m_variableSpace[key] = netId;

                        return retEntity;
                    }
                }
            }

            await BaseScript.Delay(0);

            retEntity = await GetEntity<T>(netId);

            if (m_creatingPurposes.Contains(purpose))
            {
                m_creatingPurposes.Remove(purpose);
            }

            return retEntity;
        }

        private async Task<T> GetEntity<T>(int netId) where T : Entity, new()
        {
            Pointer outPtr = typeof(int);

            do
            {
                await BaseScript.Delay(0);

                if (typeof(T) == typeof(Ped))
                {
                    Function.Call(Natives.GET_PED_FROM_NETWORK_ID, netId, outPtr);
                }
                else if (typeof(T) == typeof(Vehicle))
                {
                    Function.Call(Natives.GET_VEHICLE_FROM_NETWORK_ID, netId, outPtr);
                }

                if (SkipNullHandles && outPtr.GetValue<int>() == 0)
                {
                    return null;
                }
            } while (outPtr.GetValue<int>() == 0);

            return ObjectCache<T>.Get((int)outPtr);
        }

        private int GetNetworkId<T>(T entity) where T : Entity
        {
            Pointer outPtr = typeof(int);

            if (entity is Ped)
            {
                Function.Call(Natives.GET_NETWORK_ID_FROM_PED, entity.Handle, outPtr);
            }
            else if (entity is Vehicle)
            {
                Function.Call(Natives.GET_NETWORK_ID_FROM_VEHICLE, entity.Handle, outPtr);
            }

            return outPtr.GetValue<int>();
        }

        private async Task<T> CreateEntity<T>(Model model, Vector3 coords, float heading) where T : Entity
        {
            Entity entity = null;

            if (typeof(T) == typeof(Ped))
            {
                entity = await World.CreatePed(model, coords);
            }
            else if (typeof(T) == typeof(Vehicle))
            {
                entity = await World.CreateVehicle(model, coords);
            }
            else if (typeof(T) == typeof(GameObject))
            {
                // why is this not an entity?!
            }

            if (entity != null)
            {
                entity.Heading = heading;
            }

            return entity as T;
        }

        private List<Tuple<int, Type, Delegate>> m_controlTasks = new List<Tuple<int, Type, Delegate>>();

        public void SetControlTask<T>(T entity, Func<T, Task> onControl) where T : Entity, new()
        {
            m_controlTasks.Add(Tuple.Create<int, Type, Delegate>(GetNetworkId(entity), typeof(T), onControl));
        }

        public void SetPedTask(Ped entity, int taskId, uint taskNative, params Parameter[] parameters)
        {
            SetControlTask(entity, ped =>
            {
                Pointer statusPtr = typeof(int);

                Function.Call(Natives.GET_SCRIPT_TASK_STATUS, entity.Handle, taskId, statusPtr);

                if (statusPtr.GetValue<int>() == 7)
                {
                    var passParameters = new Parameter[] { ped.Handle }.Concat(parameters).ToArray();

                    Function.Call(taskNative, passParameters);
                }

                return Task.FromResult(false);
            });
        }

        internal async Task Tick()
        {
            foreach (var task in m_controlTasks.ToArray())
            {
                if (Function.Call<bool>(Natives.HAS_CONTROL_OF_NETWORK_ID, task.Item1))
                {
                    if (task.Item2 == typeof(Ped))
                    {
                        await (Task)task.Item3.DynamicInvoke(await GetEntity<Ped>(task.Item1));
                    }
                    else if (task.Item2 == typeof(Vehicle))
                    {
                        await (Task)task.Item3.DynamicInvoke(await GetEntity<Vehicle>(task.Item1));
                    }
                }
            }
        }
    }
}
