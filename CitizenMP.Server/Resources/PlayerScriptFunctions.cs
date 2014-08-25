using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLua;
using LuaL = KeraLua.Lua;

namespace CitizenMP.Server.Resources
{
    class PlayerScriptFunctions
    {
        [LuaFunction("GetPlayers")]
        static LuaTable GetPlayers()
        {
            var subClients = ClientInstances.Clients.Where(c => c.Value.NetChannel != null).Select(c => c.Value.NetID).ToArray();

            var L = ScriptEnvironment.CurrentEnvironment.NativeLuaState;
            var lua = ScriptEnvironment.CurrentEnvironment.LuaState;

            LuaL.LuaCreateTable(L, subClients.Length, 0);

            var table = LuaLib.LuaGetTop(L);
            var i = 1;

            foreach (var client in subClients)
            {
                lua.Push(client);
                LuaLib.LuaRawSetI(L, table, i);

                i++;
            }

            var luaTable = new LuaTable(LuaLib.LuaRef(L, 1), lua);

            return luaTable;
        }

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

        [LuaFunction("GetPlayerLastMsg")]
        static double GetPlayerLastMsg_f(int source)
        {
            var player = FindPlayer(source);

            if (player != null)
            {
                return (DateTime.UtcNow - player.LastSeen).TotalMilliseconds;
            }

            return 99999999;
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
