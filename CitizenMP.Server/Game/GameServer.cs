using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CitizenMP.Server.Game
{
    class GameServer
    {
        private Socket m_gameSocket;

        private SocketAsyncEventArgs m_asyncEventArgs;

        private byte[] m_receiveBuffer;

        private Client m_host;
        public bool UseAsync { get; set; }

        public GameServer()
        {
            UseAsync = true;
        }

        public void Start()
        {
            this.Log().Info("Starting game server on port {0}", 30120);

            m_gameSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            m_gameSocket.Bind(new IPEndPoint(IPAddress.Any, 30120));

            m_gameSocket.Blocking = false;

            m_receiveBuffer = new byte[2048];

            if (UseAsync)
            {
                m_asyncEventArgs = new SocketAsyncEventArgs();
                m_asyncEventArgs.SetBuffer(m_receiveBuffer, 0, m_receiveBuffer.Length);

                m_asyncEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.None, 0);

                m_asyncEventArgs.Completed += m_asyncEventArgs_Completed;

                if (!m_gameSocket.ReceiveFromAsync(m_asyncEventArgs))
                {
                    m_asyncEventArgs_Completed(this, m_asyncEventArgs);
                }
            }
        }

        void ProcessOOB(IPEndPoint remoteEP, byte[] buffer, int length)
        {
            var commandText = Encoding.UTF8.GetString(buffer, 4, length - 4);
            var command = commandText.Split(' ')[0];

            if (command == "connect")
            {
                ProcessConnectCommand(remoteEP, commandText);
            }
        }

        void ProcessConnectCommand(IPEndPoint remoteEP, string commandText)
        {
            var argumentString = commandText.Substring("connect ".Length);
            var arguments = Utils.ParseQueryString(argumentString);

            if (arguments.ContainsKey("token") && arguments.ContainsKey("guid"))
            {
                var clientKV = ClientInstances.Clients.FirstOrDefault(cl => cl.Value.Guid == arguments["guid"] && cl.Value.Token == arguments["token"]);

                if (clientKV.Equals(default(KeyValuePair<string,Client>)))
                {
                    return;
                }

                var client = clientKV.Value;
                client.Touch();

                client.NetChannel = new NetChannel(client);
                client.NetID = ClientInstances.AssignNetID();

                client.RemoteEP = remoteEP;
                client.Socket = m_gameSocket;

                var outString = string.Format("    connectOK {0} {1} {2}", client.NetID, (m_host != null) ? m_host.NetID : -1, (m_host != null) ? m_host.Base : -1);
                var outMessage = Encoding.ASCII.GetBytes(outString);

                outMessage[0] = 0xFF; outMessage[1] = 0xFF; outMessage[2] = 0xFF; outMessage[3] = 0xFF;

                m_gameSocket.SendTo(outMessage, remoteEP);
            }
        }

        void ProcessClientMessage(Client client, BinaryReader reader)
        {
            // touch the client
            client.Touch();

            // acknowledge any reliable commands
            var curReliableAck = reader.ReadUInt32();

            if (curReliableAck != client.OutReliableAcknowledged)
            {
                this.Log().Debug("qwack!");

                for (int i = client.OutReliableCommands.Count - 1; i >= 0; i--)
                {
                    if (client.OutReliableCommands[i].ID <= curReliableAck)
                    {
                        client.OutReliableCommands.RemoveAt(i);

                        this.Log().Debug("ack!");
                    }
                }

                client.OutReliableAcknowledged = curReliableAck;
            }

            // now read the actual message
            try
            {
                while (true)
                {
                    var messageType = reader.ReadUInt32();

                    if (messageType == 0xCA569E63) // 'msgEnd'
                    {
                        return;
                    }

                    if (messageType == 0xE938445B) // 'msgRoute'
                    {
                        ProcessRoutingMessage(client, reader);
                    } 
                    else if (messageType == 0xB3EA30DE) // 'msgIHost'
                    {
                        ProcessIHostMessage(client, reader);
                    }
                    else // reliable command
                    {
                        ProcessReliableMessage(client, messageType, reader);
                    }
                }
            }
            catch (EndOfStreamException)
            {
                this.Log().Debug("end of stream for client {0}", client.NetID);
            }
        }

        void ProcessRoutingMessage(Client client, BinaryReader reader)
        {
            client.SentData = true;

            // find the target client
            var targetNetID = reader.ReadUInt16();

            // read the actual packet first so our stream doesn't end up broken
            var dataLength = reader.ReadUInt16();
            var data = reader.ReadBytes(dataLength);

            var targetClientKV = ClientInstances.Clients.FirstOrDefault(c => c.Value.NetID == targetNetID);

            if (targetClientKV.Equals(default(KeyValuePair<string,Client>)))
            {
                this.Log().Info("no target netID {0}", targetNetID);

                return;
            }

            var targetClient = targetClientKV.Value;

            lock (targetClient)
            {
                // create a new message
                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);

                targetClient.WriteReliableBuffer(writer);

                writer.Write(0xE938445B);
                writer.Write(client.NetID);
                writer.Write(dataLength);
                writer.Write(data);
                writer.Write(0xCA569E63);

                this.Log().Debug("routing {0}-byte packet to {1}", dataLength, targetNetID);

                targetClient.NetChannel.Send(stream.ToArray());
            }
        }

        void ProcessIHostMessage(Client client, BinaryReader reader)
        {
            var baseNum = reader.ReadInt32();
            var allowHost = false;

            if (m_host == null)
            {
                allowHost = true;
            }
            else
            {
                if ((DateTime.UtcNow - m_host.LastSeen).TotalSeconds > 15)
                {
                    allowHost = true;
                }
            }

            if (allowHost)
            {
                m_host = client;
                m_host.Base = baseNum;

                var outMsg = new MemoryStream();
                var outWriter = new BinaryWriter(outMsg);

                outWriter.Write(client.NetID);
                outWriter.Write(client.Base);

                // send a is-host message to everyone
                foreach (var targetClient in ClientInstances.Clients)
                {
                    var tCl = targetClient.Value;

                    tCl.SendReliableCommand(0xB3EA30DE, outMsg.ToArray());
                }
            }
        }

        void HandleReliableCommand(Client client, uint messageType, BinaryReader reader, int size)
        {
            this.Log().Debug("that's a fairly nice {0}", messageType);

            if (messageType == 0x7337FD7A) // msgNetEvent
            {
                var targetNetID = reader.ReadUInt16();
                var nameLength = reader.ReadUInt16();

                var eventName = "";

                for (int i = 0; i < (nameLength - 1); i++)
                {
                    eventName += (char)reader.ReadByte();
                }

                // null terminator
                reader.ReadByte();

                this.Log().Debug("client sent a {0}", eventName);

                // data length
                var dataLength = size - nameLength - 4;

                // create an output packet
                var outMsg = new MemoryStream();
                var outWriter = new BinaryWriter(outMsg);

                outWriter.Write(client.NetID);
                outWriter.Write(nameLength);

                for (int i = 0; i < (nameLength - 1); i++)
                {
                    outWriter.Write((byte)eventName[i]);
                }

                outWriter.Write((byte)0);

                outWriter.Write(reader.ReadBytes(dataLength));

                var buffer = outMsg.ToArray();

                // and send it to all clients
                foreach (var targetClient in ClientInstances.Clients)
                {
                    var tCl = targetClient.Value;

                    tCl.SendReliableCommand(0x7337FD7A, buffer);
                }
            }
        }

        void ProcessReliableMessage(Client client, uint messageType, BinaryReader reader)
        {
            var id = reader.ReadUInt32();
            int size;

            if ((id & 0x80000000) != 0)
            {
                size = reader.ReadInt32();

                id &= ~0x80000000;
            }
            else
            {
                size = reader.ReadInt16();
            }

            var basePos = reader.BaseStream.Position;

            if (id > client.LastReceivedReliable)
            {
                HandleReliableCommand(client, messageType, reader, size);

                client.LastReceivedReliable = id;
            }

            // if the handler doesn't fully read, still recover position
            reader.BaseStream.Position = basePos + size;
        }

        void ProcessIncomingPacket(byte[] buffer, int length, IPEndPoint remoteEP)
        {
            using (var stream = new MemoryStream(buffer))
            {
                var reader = new BinaryReader(stream);

                var sequence = reader.ReadUInt32();

                if (sequence == 0xFFFFFFFF)
                {
                    ProcessOOB(remoteEP, buffer, length);
                }
                else
                {
                    var clientKV = ClientInstances.Clients.FirstOrDefault(c => c.Value.RemoteEP != null && c.Value.RemoteEP.Equals(remoteEP));

                    if (clientKV.Equals(default(KeyValuePair<string,Client>)))
                    {
                        this.Log().Info("Received a packet from an unknown source ({0})", remoteEP);
                        return;
                    }

                    var client = clientKV.Value;

                    if (client.NetChannel.Process(buffer, ref reader))
                    {
                        ProcessClientMessage(client, reader);
                    }
                }
            }
        }

        void m_asyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
                {
                    ProcessIncomingPacket(e.Buffer, e.BytesTransferred, (IPEndPoint)e.RemoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                this.Log().Error(() => "receive failed", ex);
            }

            // this may very well result in a stack overflow ;/
            if (!m_gameSocket.ReceiveFromAsync(e))
            {
                m_asyncEventArgs_Completed(this, e);
            }
        }

        private int m_residualTime;
        private int m_serverTime;

        public void Tick(int msec)
        {
            if (!UseAsync)
            {
                // network reception
                EndPoint receiveEP = new IPEndPoint(IPAddress.None, 0);

                while (true)
                {
                    try
                    {
                        int length = m_gameSocket.ReceiveFrom(m_receiveBuffer, ref receiveEP);

                        if (length > 0)
                        {
                            ProcessIncomingPacket(m_receiveBuffer, length, (IPEndPoint)receiveEP);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode != SocketError.WouldBlock)
                        {
                            this.Log().Warn("socket error {0}", e.Message);
                        }

                        break;
                    }
                }
            }

            // is it time for a server frame yet?
            m_residualTime += msec;

            // 20 FPS = 50msec intervals
            while (m_residualTime > 50)
            {
                m_residualTime -= 50;
                m_serverTime += 50;

                ProcessServerFrame();
            }
        }

        private int m_lastSenselessReliableSent;

        private void ProcessServerFrame()
        {
            // process client timeouts
            var curTime = DateTime.UtcNow;

            var toRemove = new List<Client>();

            foreach (var client in ClientInstances.Clients)
            {
                var timeout = (client.Value.SentData) ? 15 : 60;

                if ((curTime - client.Value.LastSeen).TotalSeconds > timeout)
                {
                    this.Log().Info("disconnected a client for timing out");

                    toRemove.Add(client.Value);
                }
            }

            foreach (var client in toRemove)
            {
                ClientInstances.RemoveClient(client);
            }

            // time for another senseless reliable?
            var sendSenselessReliable = false;

            if ((m_serverTime - m_lastSenselessReliableSent) > 5000)
            {
                sendSenselessReliable = true;

                m_lastSenselessReliableSent = m_serverTime;
            }

            // and then just send reliable buffers
            foreach (var client in ClientInstances.Clients)
            {
                var cl = client.Value;

                if (sendSenselessReliable)
                {
                    if (cl.NetChannel != null)
                    {
                        var bytes = new byte[] { 0x12, 0x34, 0x56, 0x78 };

                        cl.SendReliableCommand(0x1234, bytes);
                    }
                }

                if (cl.NetChannel != null && cl.OutReliableCommands.Count > 0)
                {
                    lock (cl)
                    {
                        // create a new message
                        var stream = new MemoryStream();
                        var writer = new BinaryWriter(stream);

                        cl.WriteReliableBuffer(writer);
                        writer.Write(0xCA569E63);

                        cl.NetChannel.Send(stream.ToArray());
                    }
                }
            }
        }
    }
}
