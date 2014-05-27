using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Formats
{
    public class RPFEntry
    {
        private List<RPFEntry> m_subEntries;

        public string Name { get; private set; }
        public bool IsDirectory { get; private set; }

        public byte[] FileData { get; private set; }

        public RPFEntry()
        {
            m_subEntries = new List<RPFEntry>();
        }

        public RPFEntry(string name, byte[] data)
            : this()
        {
            Name = name;
            FileData = data;
        }

        public RPFEntry(string name, bool isDirectory)
            : this()
        {
            Name = name;
            IsDirectory = isDirectory;
        }

        public void AddFile(string name, byte[] data)
        {
            if (data.Length >= 4 && data[0] == 'R' && data[1] == 'S' && data[2] == 'C')
            {
                throw new InvalidOperationException("Resource files are currently not supported.");
            }

            var pathParts = new Queue<string>(name.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries));

            if (pathParts.Count == 1)
            {
                m_subEntries.Add(new RPFEntry(name, data));
            }
            else
            {
                var directory = FindDirectory(pathParts, "");

                directory.m_subEntries.Add(new RPFEntry(name, data));
            }
        }

        private RPFEntry FindDirectory(Queue<string> queue, string basePath)
        {
            var path = queue.Dequeue();
            var entry = m_subEntries.Find(e => e.Name == basePath + path);

            if (entry != null)
            {
                if (!entry.IsDirectory)
                {
                    throw new InvalidOperationException("This already is a file, so a directory can't be made.");
                }                
            }
            else
            {
                entry = new RPFEntry(basePath + path, true);

                m_subEntries.Add(entry);
            }

            if (queue.Count > 1)
            {
                entry = entry.FindDirectory(queue, basePath + path + "/");
            }

            return entry;
        }

        internal void Write(MarkedBinaryWriter writer)
        {
            // write our entry
            //if (Name != "")
            {
                writer.Mark("nOff_" + Name);
                writer.Write(0);

                if (!IsDirectory)
                {
                    writer.Write(FileData.Length);

                    writer.Mark("fOff_" + Name); // also should contain the resource type
                    writer.Write(0);

                    writer.Write(FileData.Length); // no flags, but apparently size?
                }
                else
                {
                    writer.Write(m_subEntries.Count);

                    writer.Mark("cIdx_" + Name);
                    writer.Write(0);

                    writer.Write(m_subEntries.Count);
                }

                writer.WriteIdx++;
            }
        }

        internal void WriteSubEntries(MarkedBinaryWriter writer)
        {
            if (IsDirectory)
            {
                writer.WriteMark("cIdx_" + Name, (uint)writer.WriteIdx | 0x80000000);
            }

            var subEntriesSorted = m_subEntries.OrderBy(e => e.Name);

            foreach (var entry in subEntriesSorted)
            {
                entry.Write(writer);
            }

            foreach (var entry in subEntriesSorted)
            {
                entry.WriteSubEntries(writer);
            }
        }

        internal void WriteNames(MarkedBinaryWriter writer)
        {
            if (Name != "")
            {
                writer.WriteMark("nOff_" + Name, (uint)writer.BaseStream.Position - (uint)writer.BaseNamePosition);
                writer.Write(Encoding.ASCII.GetBytes(System.IO.Path.GetFileName(Name)));
                writer.Write((byte)0);
            }
            else
            {
                writer.BaseNamePosition = (int)writer.BaseStream.Position;

                writer.WriteMark("nOff_", 0);
                writer.Write(new byte[] { (byte)'/', 0 });
            }

            foreach (var entry in m_subEntries)
            {
                entry.WriteNames(writer);
            }
        }

        internal void WriteFiles(MarkedBinaryWriter writer)
        {
            if (!IsDirectory)
            {
                writer.WriteMark("fOff_" + Name, (uint)writer.BaseStream.Position & 0xffffff00);

                writer.Write(FileData);

                writer.Align(2048);
            }

            foreach (var entry in m_subEntries)
            {
                entry.WriteFiles(writer);
            }
        }
    }
}
