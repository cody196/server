using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using uhttpsharp;
using uhttpsharp.Headers;
using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.HTTP
{
    static class InitConnectMethod
    {
        public static Func<IHttpHeaders, IHttpContext, Task<JObject>> Get(Game.GameServer gameServer)
        {
            return async (headers, context) =>
            {
                var result = new JObject();

                var name = headers.GetByName("name");
                var guid = headers.GetByName("guid");

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(guid))
                {
                    result["err"] = "fields missing";

                    return result;
                }

                if (!gameServer.Configuration.DisableAuth)
                {
                    string authTicket;

                    if (!headers.TryGetByName("authTicket", out authTicket))
                    {
                        result["authID"] = gameServer.PlatformClient.LoginId;

                        return result;
                    }

                    var ipUInt = (uint)IPAddress.HostToNetworkOrder(BitConverter.ToInt32(((IPEndPoint)context.RemoteEndPoint).Address.GetAddressBytes(), 0));

                    var authResult = await gameServer.PlatformClient.ValidateTicket(new IPAddress(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(ipUInt))), ulong.Parse(guid), new NPSharp.RPC.Messages.Data.Ticket(Convert.FromBase64String(authTicket)));

                    if (!authResult)
                    {
                        result["error"] = "Invalid NPID sent.";

                        return result;
                    }
                }

                var client = new Client();
                client.Token = TokenGenerator.GenerateToken();
                client.Name = name;
                client.Guid = ulong.Parse(guid).ToString("x16");
                client.Touch();

                if (ClientInstances.Clients.ContainsKey(guid))
                {
                    gameServer.DropClient(ClientInstances.Clients[guid], "Duplicate GUID");
                }

                ClientInstances.AddClient(client);

                result["token"] = client.Token;

                return result;
            };
        }
    }
}
