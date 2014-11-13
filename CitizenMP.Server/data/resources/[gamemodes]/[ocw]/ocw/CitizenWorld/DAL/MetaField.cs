using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MsgPack;

namespace CitizenWorld.DAL
{
    class MetaField : IPackable
    {
        public string FieldId { get; private set; }

        public MetaField(string id)
        {
            FieldId = id;
        }

        public void PackToMessage(Packer packer, PackingOptions options)
        {
            packer.PackMapHeader(2);
            packer.Pack("__type");
            packer.Pack("MetaField");

            packer.Pack("fieldId");
            packer.Pack(FieldId);
        }
    }
}
