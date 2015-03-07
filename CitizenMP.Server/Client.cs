using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server
{
    public class Client
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public string Token { get; set; }

        public ushort NetID { get; set; }

        public uint ProtocolVersion { get; set; }

        public int Base { get; set; }

        public uint OutReliableSequence { get; set; }
        public uint OutReliableAcknowledged { get; set; }
        public uint LastReceivedReliable { get; set; }

        public List<OutReliableCommand> OutReliableCommands { get; set; }

        public IPEndPoint RemoteEP { get; set; }

        public Game.NetChannel NetChannel { get; set; }

        public long LastSeen { get; private set; }

        public bool SentData { get; set; }

        public Socket Socket { get; set; }

        public uint FrameNumber { get; set; }

        public uint LastReceivedFrame { get; set; }

        public ClientFrame[] Frames { get; set; }

        public int Ping { get; private set; }

        public IEnumerable<string> Identifiers { get; set; }

        public Client()
        {
            OutReliableCommands = new List<OutReliableCommand>();
            ProtocolVersion = 1;

            Frames = new ClientFrame[32];
        }

        public void Touch()
        {
            LastSeen = Time.CurrentTime;
        }

        public void CalculatePing()
        {
            int pingTotal = 0, pingCount = 0;

            for (int i = 0; i < Frames.Length; i++)
            {
                if (Frames[i].AckedTime <= 0)
                {
                    continue;
                }

                pingTotal += (int)(Frames[i].AckedTime - Frames[i].SentTime);
                pingCount++;
            }

            if (pingCount == 0)
            {
                Ping = -1;
            }
            else
            {
                Ping = pingTotal / pingCount;
            }
        }

        public void WriteReliableBuffer(BinaryWriter writer)
        {
            writer.Write(LastReceivedReliable);

            lock (OutReliableCommands)
            {
                var outReliableCommands = OutReliableCommands.GetRange(0, OutReliableCommands.Count);

                foreach (var cmd in outReliableCommands)
                {
                    if (cmd.Command == null)
                    {
                        continue;
                    }

                    writer.Write(cmd.Type);

                    if (cmd.Command.Length > ushort.MaxValue)
                    {
                        writer.Write(cmd.ID | 0x80000000);
                        writer.Write(cmd.Command.Length);
                    }
                    else
                    {
                        writer.Write(cmd.ID);
                        writer.Write((ushort)cmd.Command.Length);
                    }

                    writer.Write(cmd.Command);
                }
            }
        }

        public void SendReliableCommand(uint commandType, byte[] commandData)
        {
            lock (OutReliableCommands)
            {
                OutReliableCommands.Add(new OutReliableCommand() { ID = OutReliableSequence + 1, Command = commandData, Type = commandType });
            }

            OutReliableSequence++;
        }

        public void SendRaw(byte[] buffer)
        {
            //this.Log().Debug("sending {0}-byte packet to {1}", buffer.Length, NetID);

            if (buffer.Length > 1400)
            {
                this.Log().Error("THIS IS BAD");
            }

            if (Socket == null)
            {
                return;
            }

            try
            {
                Socket.SendTo(buffer, RemoteEP);
            }
            catch (SocketException)
            { }
        }
    }

    public struct ClientFrame
    {
        public long SentTime { get; set; }
        public long AckedTime { get; set; }
    }

    public struct OutReliableCommand
    {
        public uint ID { get; set; }

        public uint Type { get; set; }

        public byte[] Command { get; set; }
    }
}
