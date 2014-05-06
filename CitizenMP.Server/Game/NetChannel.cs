using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Game
{
    public class NetChannel
    {
        private Client m_client;

        private const int FRAGMENT_SIZE = 1300;

        private uint m_fragmentSequence;
        private int m_fragmentLength;
        private MemoryStream m_fragmentBuffer;
        private uint m_inSequence;
        private uint m_outSequence;
        
        public NetChannel(Client client)
        {
            m_client = client;
        }

        public bool Process(byte[] buffer, ref BinaryReader reader)
        {
            var sequence = BitConverter.ToUInt32(buffer, 0);

            var fragmented = ((sequence & 0x80000000) != 0);
            var fragmentStart = 0;
            var fragmentLength = 0;

            if (fragmented)
            {
                fragmentStart = BitConverter.ToInt16(buffer, 4);
                fragmentLength = BitConverter.ToInt16(buffer, 6);

                sequence &= ~0x80000000;
            }

            if (sequence <= m_inSequence && m_inSequence != 0)
            {
                this.Log().Debug("out-of-order packet");
                return false;
            }

            if (sequence > (m_inSequence + 1))
            {
                this.Log().Debug("dropped packet");
            }

            if (fragmented)
            {
                if (sequence != m_fragmentSequence)
                {
                    m_fragmentLength = 0;
                    m_fragmentSequence = sequence;
                    m_fragmentBuffer = new MemoryStream();
                }

                if (fragmentStart != m_fragmentLength)
                {
                    return false;
                }

                if (fragmentLength < 0)
                {
                    return false;
                }

                m_fragmentBuffer.Write(buffer, 8, buffer.Length - 8);

                if (fragmentLength == FRAGMENT_SIZE)
                {
                    return false;
                }

                m_inSequence = sequence;
                m_fragmentLength = 0;

                reader = new BinaryReader(m_fragmentBuffer);

                return true;
            }

            m_inSequence = sequence;

            return true;
        }

        public void Send(byte[] buffer)
        {
            if (buffer.Length > FRAGMENT_SIZE)
            {
                SendFragmented(buffer);
                return;
            }

            var targetBuffer = new byte[buffer.Length + 4];
            Array.Copy(BitConverter.GetBytes(m_outSequence), targetBuffer, 4);
            Array.Copy(buffer, 0, targetBuffer, 4, buffer.Length);

            m_client.SendRaw(targetBuffer);

            m_outSequence++;
        }

        private void SendFragmented(byte[] buffer)
        {
            var outSequence = m_outSequence | 0x80000000;

            var remaining = buffer.Length;
            var start = 0;

            while (remaining >= 0)
            {
                var thisSize = remaining;

                if (thisSize > FRAGMENT_SIZE)
                {
                    thisSize = FRAGMENT_SIZE;
                }

                var targetBuffer = new byte[buffer.Length + 8];
                Array.Copy(BitConverter.GetBytes(outSequence), targetBuffer, 4);
                Array.Copy(BitConverter.GetBytes((ushort)start), 0, targetBuffer, 4, 2);
                Array.Copy(BitConverter.GetBytes((ushort)thisSize), 0, targetBuffer, 6, 2);
                Array.Copy(buffer, 0, targetBuffer, 8, buffer.Length);

                m_client.SendRaw(targetBuffer);

                remaining -= thisSize;
                start += thisSize;

                if (remaining == 0 && thisSize != FRAGMENT_SIZE)
                {
                    break;
                }
            }

            m_outSequence++;
        }
    }
}
