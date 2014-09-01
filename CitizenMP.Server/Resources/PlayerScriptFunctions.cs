using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neo.IronLua;

namespace CitizenMP.Server.Resources
{
    class PlayerScriptFunctions
    {
        [LuaMember("GetPlayers")]
        static LuaTable GetPlayers()
        {
            var subClients = ClientInstances.Clients.Where(c => c.Value.NetChannel != null).Select(c => c.Value.NetID).ToArray();

            var table = new LuaTable();
            var i = 0;

            foreach (var client in subClients)
            {
                table[i] = client;

                i++;
            }

            return table;
        }

        [LuaMember("GetPlayerName")]
        static string GetPlayerName(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return player.Name;
            }

            return null;
        }

        [LuaMember("GetPlayerGuid")]
        static string GetPlayerGuid(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return player.Guid.PadLeft(16, '0');
            }

            return null;
        }

        [LuaMember("GetPlayerEP")]
        static string GetPlayerEP(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return player.RemoteEP.ToString();
            }

            return null;
        }

        [LuaMember("GetPlayerLastMsg")]
        static double GetPlayerLastMsg_f(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return (DateTime.UtcNow - player.LastSeen).TotalMilliseconds;
            }

            return 99999999;
        }

        [LuaMember("GetHostId")]
        static int GetHostId()
        {
            return ScriptEnvironment.CurrentEnvironment.Resource.Manager.GameServer.GetHostID();
        }

        static Client FindPlayer(int source)
        {
            return ClientInstances.Clients.Where(a => a.Value.NetID == source).Select(a => a.Value).FirstOrDefault();
        }
    }
}
