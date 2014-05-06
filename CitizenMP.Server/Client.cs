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

        public int Base { get; set; }

        public uint OutReliableSequence { get; set; }
        public uint OutReliableAcknowledged { get; set; }
        public uint LastReceivedReliable { get; set; }

        public List<OutReliableCommand> OutReliableCommands { get; set; }

        public IPEndPoint RemoteEP { get; set; }

        public Game.NetChannel NetChannel { get; set; }

        public DateTime LastSeen { get; private set; }

        public bool SentData { get; set; }

        public Socket Socket { get; set; }

        public Client()
        {
            OutReliableCommands = new List<OutReliableCommand>();
        }

        public void Touch()
        {
            LastSeen = DateTime.UtcNow;
        }

        public void WriteReliableBuffer(BinaryWriter writer)
        {
            writer.Write(LastReceivedReliable);

            foreach (var cmd in OutReliableCommands)
            {
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

        public void SendReliableCommand(uint commandType, byte[] commandData)
        {
            OutReliableCommands.Add(new OutReliableCommand() { ID = OutReliableSequence + 1, Command = commandData, Type = commandType });
            OutReliableSequence++;
        }

        public void SendRaw(byte[] buffer)
        {
            this.Log().Debug("sending {0}-byte packet to {1}", buffer.Length, NetID);

            if (buffer.Length > 1024)
            {
                this.Log().Error("THIS IS BAD");
            }

            Socket.SendTo(buffer, RemoteEP);
        }
    }

    public struct OutReliableCommand
    {
        public uint ID { get; set; }

        public uint Type { get; set; }

        public byte[] Command { get; set; }
    }
}
