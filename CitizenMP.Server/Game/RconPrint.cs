using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Game
{
    class RconPrint
    {
        [ThreadStatic]
        static StringBuilder ms_outBuffer;

        [ThreadStatic]
        static IPEndPoint ms_endPoint;

        [ThreadStatic]
        static GameServer ms_gameServer;

        public static void StartRedirect(GameServer gs, IPEndPoint ep)
        {
            ms_outBuffer = new StringBuilder();
            ms_endPoint = ep;
            ms_gameServer = gs;
        }

        public static void Print(string str, params object[] args)
        {
            if (ms_outBuffer == null)
            {
                return;
            }

            if ((ms_outBuffer.Length + str.Length) > 1000)
            {
                Flush();
            }

            ms_outBuffer.AppendFormat(str, args);
        }

        public static void EndRedirect()
        {
            Flush();

            ms_outBuffer = null;
        }

        private static void Flush()
        {
            ms_gameServer.SendOutOfBand(ms_endPoint, "print\n{0}", ms_outBuffer.ToString());
            ms_outBuffer.Clear();
        }
    }
}
