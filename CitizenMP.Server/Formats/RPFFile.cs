using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Formats
{
    public class RPFFile
    {
        private RPFEntry RootEntry { get; set; }

        public RPFFile()
        {
            RootEntry = new RPFEntry("", true);
        }

        public void AddFile(string name, byte[] data)
        {
            RootEntry.AddFile(name, data);
        }

        public void Write(string path)
        {
            var stream = File.Open(path, FileMode.Create);
            var writer = new MarkedBinaryWriter(stream);

            writer.Write(0x32465052); // RPF2

            writer.Mark("tocSize");
            writer.Write(0);

            writer.Mark("numEntries");
            writer.Write(0);

            writer.Write(0);
            writer.Write(0); // not encrypted; for now

            writer.Align(2048);

            // write a TOC for the file
            RootEntry.Write(writer);
            RootEntry.WriteSubEntries(writer);
            RootEntry.WriteNames(writer);

            writer.WriteMark("numEntries", (uint)writer.WriteIdx);

            writer.Align(2048);

            // this has to be aligned or it will be cut off by 16 byte align
            writer.WriteMark("tocSize", (uint)stream.Position - 2048);

            RootEntry.WriteFiles(writer);

            writer.Close();
        }
    }
}
