using System;
using System.Collections;
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
        private byte[] m_fragmentBuffer;
        private BitArray m_fragmentValidSet;
        private int m_fragmentLastBit;
        private uint m_inSequence;
        private uint m_outSequence;
        
        public NetChannel(Client client)
        {
            m_client = client;
            m_fragmentSequence = 0xFFFFFFFF;
        }

        public bool Process(byte[] buffer, int length, ref BinaryReader reader)
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
                    m_fragmentBuffer = new byte[65536];
                    m_fragmentValidSet = new BitArray(65536 / FRAGMENT_SIZE);
                    m_fragmentLastBit = -1;
                }

                int fragmentBit = fragmentStart / FRAGMENT_SIZE;

                if (fragmentBit > ((65536 / FRAGMENT_SIZE) - 1))
                {
                    return false;
                }

                if (m_fragmentValidSet.Get(fragmentBit))
                {
                    return false;
                }

                m_fragmentValidSet.Set(fragmentBit, true);

                // append to the buffer
                Array.Copy(buffer, 8, m_fragmentBuffer, fragmentBit * FRAGMENT_SIZE, length - 8);
                m_fragmentLength += length - 8;

                if (m_fragmentLength != FRAGMENT_SIZE)
                {
                    m_fragmentLastBit = fragmentBit;
                }

                // check the bits to see if we got the full message
                if (m_fragmentLastBit == -1)
                {
                    return false;
                }

                for (int i = 0; i <= m_fragmentLastBit; i++)
                {
                    if (!m_fragmentValidSet.Get(i))
                    {
                        return false;
                    }
                }

                m_inSequence = sequence;

                reader = new BinaryReader(new MemoryStream(m_fragmentBuffer, 0, m_fragmentLength));

                m_fragmentLength = 0;

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

                var targetBuffer = new byte[thisSize + 8];
                Array.Copy(BitConverter.GetBytes(outSequence), targetBuffer, 4);
                Array.Copy(BitConverter.GetBytes((ushort)start), 0, targetBuffer, 4, 2);
                Array.Copy(BitConverter.GetBytes((ushort)thisSize), 0, targetBuffer, 6, 2);
                Array.Copy(buffer, start, targetBuffer, 8, thisSize);

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
