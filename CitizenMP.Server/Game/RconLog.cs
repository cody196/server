using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using uhttpsharp;

namespace CitizenMP.Server.Game
{
    class RconLog
    {
        private List<string> m_entries = new List<string>();

        private long m_startTime;

        public RconLog()
        {
            m_startTime = Time.CurrentTime;
        }

        public void Append(string str)
        {
            var jobj = JObject.Parse(str);
            jobj["msgTime"] = (int)(Time.CurrentTime - m_startTime);

            m_entries.Add(jobj.ToString(Formatting.None));
        }

        public void RunHttp(IHttpContext context)
        {
            var entryString = string.Join("\n", m_entries);
            string range;

            if (context.Request.Headers.TryGetByName("range", out range))
            {
                if (range.StartsWith("bytes="))
                {
                    var bits = range.Substring(6).Split('-');
                    var start = int.Parse(bits[0]);
                    var end = int.Parse(bits[1]);

                    entryString = entryString.Substring(start, end - start);
                }
            }

            context.Response = new HttpResponse(HttpResponseCode.Ok, "text/plain", entryString, true);

            //m_entries.Clear();
        }
    }
}
