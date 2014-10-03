using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neo.IronLua;

namespace CitizenMP.Server.Resources
{
    class HttpScriptFunctions
    {
        [LuaMember("PerformHttpRequest")]
        static async Task PerformHttpRequest(string url, Func<object, object, object, LuaResult> cb, string method = "GET", string data = "", LuaTable headers = null)
        {
            var webRequest = HttpWebRequest.CreateHttp(url);
            webRequest.Method = method;

            var senv = ScriptEnvironment.CurrentEnvironment;
            
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    webRequest.Headers.Add(header.Key.ToString(), header.Value.ToString());
                }
            }

            try
            {
                if (data != string.Empty)
                {
                    var reqStream = await webRequest.GetRequestStreamAsync();
                    var dataBytes = Encoding.UTF8.GetBytes(data);

                    await reqStream.WriteAsync(dataBytes, 0, dataBytes.Length);
                }

                var response = await webRequest.GetResponseAsync() as HttpWebResponse;

                // store headers
                var respHeaders = new LuaTable();
                
                for (int i = 0; i < response.Headers.Count; i++)
                {
                    respHeaders[response.Headers.Keys[i]] = response.Headers[i];
                }

                // get the response
                var respStream = response.GetResponseStream();
                var streamReader = new StreamReader(respStream);

                var responseText = await streamReader.ReadToEndAsync();

                try
                {
                    var penv = ScriptEnvironment.PushEnvironment(senv);
                    cb(0, responseText, respHeaders);
                    penv.PopEnvironment();
                }
                catch (Exception e)
                {
                    webRequest.Log().Error("Error in callback for web request: {0}", e.Message);

                    ScriptEnvironment.PrintLuaStackTrace(e);
                }
            }
            catch (WebException e)
            {
                webRequest.Log().Warn("Web request to {0} failed: {1}", url, e.Message);

                try
                {
                    var penv = ScriptEnvironment.PushEnvironment(senv);
                    cb((int)((HttpWebResponse)e.Response).StatusCode, null, null);
                    penv.PopEnvironment();
                }
                catch (Exception e2)
                {
                    webRequest.Log().Error("Error in callback for web request: {0}", e2.Message);

                    ScriptEnvironment.PrintLuaStackTrace(e2);
                }
            }            
        }
    }
}
