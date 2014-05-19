using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Resources
{
    class PlayerScriptFunctions
    {
        [LuaFunction("GetPlayerName")]
        static string GetPlayerName(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return player.Name;
            }

            return null;
        }

        [LuaFunction("GetPlayerGuid")]
        static string GetPlayerGuid(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return player.Guid.PadLeft(16, '0');
            }

            return null;
        }

        [LuaFunction("GetPlayerEP")]
        static string GetPlayerEP(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return player.RemoteEP.ToString();
            }

            return null;
        }

        [LuaFunction("GetHostId")]
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
