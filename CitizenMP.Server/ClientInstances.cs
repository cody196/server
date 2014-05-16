using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server
{
    static class ClientInstances
    {
        private static ConcurrentDictionary<string, Client> ms_clients = new ConcurrentDictionary<string,Client>();

        public static ReadOnlyDictionary<string, Client> Clients { get; private set; }

        static ClientInstances()
        {
            Clients = new ReadOnlyDictionary<string, Client>(ms_clients);
        }

        public static void AddClient(Client client)
        {
            ms_clients[client.Guid] = client;
        }

        private static ushort ms_curNetID;
        private static object ms_netIDLock = new object();

        public static ushort AssignNetID()
        {
            lock (ms_netIDLock)
            {
                var free = false;

                while (!free)
                {
                    ms_curNetID++;

                    if (ms_curNetID == 0 || ms_curNetID == 65535)
                    {
                        ms_curNetID = 1;
                    }

                    free = !Clients.Any(c => c.Value.NetID == ms_curNetID);
                }

                return ms_curNetID;
            }
        }

        public static void RemoveClient(Client client)
        {
            Client cl;
            ms_clients.TryRemove(client.Guid, out cl);
        }
    }
}
