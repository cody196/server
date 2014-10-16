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

                string protocol = null;
                
                headers.TryGetByName("protocol", out protocol);
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(guid))
                {
                    result["err"] = "fields missing";

                    return result;
                }

                if (string.IsNullOrEmpty(protocol))
                {
                    protocol = "1";
                }

                // check the protocol version
                uint protocolNum;

                if (!uint.TryParse(protocol, out protocolNum))
                {
                    result["err"] = "invalid protocol version";

                    return result;
                }

                // authentication
                if (!gameServer.Configuration.DisableAuth)
                {
                    string authTicket;

                    if (!headers.TryGetByName("authTicket", out authTicket))
                    {
                        result["authID"] = gameServer.PlatformClient.LoginId;

                        return result;
                    }

                    var validationAddress = ((IPEndPoint)context.RemoteEndPoint).Address;

                    if (validationAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        validationAddress = IPAddress.Parse("192.168.1.1"); // as these are whitelisted in NP code
                    }

                    var authResult = await gameServer.PlatformClient.ValidateTicket(validationAddress, ulong.Parse(guid), new NPSharp.RPC.Messages.Data.Ticket(Convert.FromBase64String(authTicket)));

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
                client.ProtocolVersion = protocolNum;
                client.Touch();

                if (ClientInstances.Clients.ContainsKey(guid))
                {
                    gameServer.DropClient(ClientInstances.Clients[guid], "Duplicate GUID");
                }

                ClientInstances.AddClient(client);

                result["token"] = client.Token;
                result["protocol"] = Game.GameServer.PROTOCOL_VERSION;

                return result;
            };
        }
    }
}
