using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using uhttpsharp;
using uhttpsharp.Handlers;
using uhttpsharp.Headers;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;

using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.HTTP
{
    class HttpServer
    {
        private Dictionary<string, Func<IHttpHeaders, JObject>> m_handlers;

        public HttpServer()
        {
            m_handlers = new Dictionary<string, Func<IHttpHeaders, JObject>>();

            m_handlers["initconnect"] = InitConnectMethod.Get();
            m_handlers["getconfiguration"] = GetConfigurationMethod.Get();
        }

        public void Start()
        {
            log4net.Config.XmlConfigurator.Configure();

            this.Log().Info("Starting HTTP server on port {0}", 30120);

            var httpServer = new uhttpsharp.HttpServer(new HttpRequestProvider());

            httpServer.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Any, 30120)));

            httpServer.Use(new HttpRouter().With("client", new AnonymousHttpRequestHandler((context, next) =>
            {
                HttpResponseCode responseCode;
                JObject result;

                if (context.Request.Method != HttpMethods.Post)
                {
                    responseCode = HttpResponseCode.BadRequest;

                    result = new JObject();
                    result["err"] = "wasn't a POST";
                }
                else
                {
                    var postData = context.Request.Post.Parsed;
                    var method = postData.GetByName("method").ToLowerInvariant();

                    if (m_handlers.ContainsKey(method))
                    {
                        result = m_handlers[method](postData);
                    }
                    else
                    {
                        result = new JObject();
                        result["err"] = "invalid method";
                    }

                    responseCode = HttpResponseCode.Ok;
                }

                context.Response = new HttpResponse(responseCode, "application/json", result.ToString(), true);

                return Task.Factory.GetCompleted();
            })));

            httpServer.Use((context, next) =>
            {
                context.Response = HttpResponse.CreateWithMessage(HttpResponseCode.NotFound, "not found", false);
                return Task.Factory.GetCompleted();
            });

            httpServer.Start();
        }
    }
}
