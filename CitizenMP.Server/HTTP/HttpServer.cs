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

        private Resources.ResourceManager m_resourceManager;

        private Configuration m_configuration;

        public HttpServer(Configuration config, Resources.ResourceManager resManager)
        {
            m_configuration = config;
            m_resourceManager = resManager;

            m_handlers = new Dictionary<string, Func<IHttpHeaders, JObject>>();

            m_handlers["initconnect"] = InitConnectMethod.Get();
            m_handlers["getconfiguration"] = GetConfigurationMethod.Get(resManager);
        }

        public void Start()
        {
            log4net.Config.XmlConfigurator.Configure();

            this.Log().Info("Starting HTTP server on port {0}", m_configuration.ListenPort);

            var httpServer = new uhttpsharp.HttpServer(new HttpRequestProvider());

            httpServer.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Any, m_configuration.ListenPort)));

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
            })).With("files", new AnonymousHttpRequestHandler((context, next) =>
            {
                var urlParts = context.Request.Uri.OriginalString.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (urlParts.Length >= 3)
                {
                    var resourceName = urlParts[1];
                    var resource = m_resourceManager.GetResource(resourceName);

                    if (resource != null)
                    {
                        if (urlParts[2] == "resource.rpf")
                        {
                            context.Response = new HttpResponse(HttpResponseCode.Ok, "application/x-rockstar-rpf", resource.OpenClientPackage(), true);
                        }
                    }
                }

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
