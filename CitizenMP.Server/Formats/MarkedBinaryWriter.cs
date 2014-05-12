using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Formats
{
    class MarkedBinaryWriter : BinaryWriter
    {
        private Dictionary<string, long> m_markOffsets;

        public MarkedBinaryWriter(Stream stream)
            : base(stream)
        {
            m_markOffsets = new Dictionary<string, long>();

            if (!stream.CanSeek)
            {
                throw new ArgumentException("This stream does not support seeking.");
            }
        }

        public int WriteIdx { get; set; }

        public int BaseNamePosition { get; set; }

        public void Mark(string markName)
        {
            m_markOffsets[markName] = BaseStream.Position;
        }

        public void WriteMark(string markName, uint value)
        {
            var curPos = BaseStream.Position;
            BaseStream.Position = m_markOffsets[markName];

            Write(value);

            BaseStream.Position = curPos;

            m_markOffsets.Remove(markName);
        }

        public void Align(int alignment)
        {
            var pos = BaseStream.Position;
            var difference = ((pos % alignment) == 0) ? 0 : (alignment - (pos % alignment));

            Write(new byte[difference]);
        }

        public override void Close()
        {
            if (m_markOffsets.Count > 0)
            {
                throw new InvalidOperationException("Can't close when there's open marks...");
            }

            base.Close();
        }
    }
}
