using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace CitizenWorld.DAL
{
    class Data<T> where T : DataObject<T>
    {
        public class EventSink : BaseScript
        {
            private Dictionary<string, TaskCompletionSource<dynamic>> m_results = new Dictionary<string, TaskCompletionSource<dynamic>>();

            public EventSink()
            {
                var retEventName = string.Format("{0}:ret{1}", DataManager.Namespace, typeof(T).Name);
                var saveEventName = string.Format("{0}:retSave{1}", DataManager.Namespace, typeof(T).Name);

                Action<string> addEventHandler = eventName =>
                {
                    EventHandlers[eventName] += new Action<dynamic>(result =>
                    {
                        TaskCompletionSource<dynamic> tcs;

                        if (m_results.TryGetValue(result.queryId, out tcs))
                        {
                            tcs.SetResult(result);

                            m_results.Remove(result.queryId);
                        }
                    });
                };

                addEventHandler(retEventName);
                addEventHandler(saveEventName);
            }

            public string GetQueryId()
            {
                return Guid.NewGuid().ToString();
            }

            public Task<dynamic> PendGet(string queryId)
            {
                var tcs = new TaskCompletionSource<dynamic>();
                m_results[queryId] = tcs;
                return tcs.Task;
            }

            public Task<dynamic> PendSave(string queryId)
            {
                var tcs = new TaskCompletionSource<dynamic>();
                m_results[queryId] = tcs;
                return tcs.Task;
            }
        }

        private static EventSink ms_eventSink;

        private static EventSink GetEventSink()
        {
            if (ms_eventSink == null)
            {
                ms_eventSink = new EventSink();
                BaseScript.RegisterScript(ms_eventSink);
            }

            return ms_eventSink;
        }

        public static async Task<IEnumerable<T>> GetAsync()
        {
            return await GetAsync(null);
        }

        public static async Task<IEnumerable<T>> GetAsync(object where)
        {
            var dataNamespace = DataManager.Namespace;
            var eventSink = GetEventSink();
            var queryId = eventSink.GetQueryId();

            BaseScript.TriggerServerEvent(string.Format("{0}:get{1}", dataNamespace, typeof(T).Name), new
            {
                version = 1,
                id = queryId,
                query = where
            });

            var result = await eventSink.PendGet(queryId);

            if (result.version != 1)
            {
                throw new Exception("Result version did not match the expected value.");
            }

            return Deserialize(result.data);
        }

        public static async Task<bool> SaveObjectAsync(T obj)
        {
            var dataNamespace = DataManager.Namespace;
            var eventSink = GetEventSink();
            var queryId = eventSink.GetQueryId();

            BaseScript.TriggerServerEvent(string.Format("{0}:save{1}", dataNamespace, typeof(T).Name), new
            {
                version = 1,
                id = queryId,
                data = obj
            });

            var result = await eventSink.PendSave(queryId);

            if (result.version != 1)
            {
                return false;
            }

            if (result.err != 0)
            {
                return false;
            }

            obj._id = result.id;
            obj._rev = result.rev;

            return true;
        }

        private static IEnumerable<T> Deserialize(dynamic result)
        {
            var resultList = new List<T>();
            var resultType = typeof(T);

            foreach (var entry in result)
            {
                var entryDict = entry as IDictionary<string, object>;

                if (entryDict == null)
                {
                    continue;
                }

                var resultEntry = Activator.CreateInstance<T>();
                
                foreach (var field in entryDict)
                {
                    var property = resultType.GetProperty(field.Key);

                    if (property == null)
                    {
                        continue;
                    }

                    property.SetValue(resultEntry, Convert.ChangeType(field.Value, property.PropertyType, System.Globalization.CultureInfo.CurrentCulture), null);
                }

                resultList.Add(resultEntry);
            }

            return resultList;
        }
    }
}
