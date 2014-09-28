using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Resources.Tasks
{
    class BuildAssemblyTask : ResourceTask
    {
        public override IEnumerable<string> DependsOn
        {
            get { return new string[0]; }
        }

        public override bool NeedsExecutionFor(Resource resource)
        {
            return false;
        }

        public override bool Process(Resource resource)
        {
            return true;
        }
    }
}
