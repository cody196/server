using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using uhttpsharp;

namespace CitizenMP.Server.Game
{
    class RconLog
    {
        private MemoryStream m_dataStream;

        private TextWriter m_textWriter;

        private long m_startTime;

        public RconLog()
        {
            m_startTime = Time.CurrentTime;

            m_dataStream = new MemoryStream();
            m_textWriter = new StreamWriter(m_dataStream);
        }

        public void Append(string str)
        {
            var jobj = JObject.Parse(str);
            jobj["msgTime"] = (int)(Time.CurrentTime - m_startTime);

            m_textWriter.WriteLine(jobj.ToString(Formatting.None));
            m_textWriter.Flush();
        }

        public void RunHttp(IHttpContext context)
        {
            string range;
            Stream retStream = m_dataStream;

            if (context.Request.Headers.TryGetByName("range", out range))
            {
                if (range.StartsWith("bytes="))
                {
                    var bits = range.Substring(6).Split('-');
                    var start = int.Parse(bits[0]);
                    var end = int.Parse(bits[1]);

                    retStream = new PartialStream(m_dataStream, start, end - start);
                }
            }

            context.Response = new HttpResponse(HttpResponseCode.Ok, "text/plain", retStream, true, false);

            //m_entries.Clear();
        }
    }

    class PartialStream : Stream
    {
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public Stream BaseStream { get; private set; }

        public long RangeStart { get; private set; }

        private long m_length;

        public override long Length
        {
            get
            {
                return m_length;
            }
        }

        public override long Position
        {
            get;
            set;
        }

        public PartialStream(Stream baseStream, long rangeStart, long length)
        {
            BaseStream = baseStream;
            RangeStart = rangeStart;
            m_length = length;

            if (!baseStream.CanSeek)
            {
                throw new ArgumentException("PartialStream requires a seekable stream.", "baseStream");
            }

            if (baseStream.Length < (rangeStart + length))
            {
                throw new ArgumentException("Stream does not contain the requested range.", "baseStream");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // store and set position
            long oldPosition = BaseStream.Position;

            BaseStream.Position = Position + RangeStart;

            // read
            int length = BaseStream.Read(buffer, offset, count);

            Position += length;

            // restore and return
            BaseStream.Position = oldPosition;

            return length;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                Position = offset;
            }
            else if (origin == SeekOrigin.Current)
            {
                Position += offset;
            }
            else if (origin == SeekOrigin.End)
            {
                Position = Length - offset;
            }

            return Position;
        }
    }
}
