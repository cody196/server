using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace CitizenWorld
{
    public class JobManager : BaseScript
    {
        private List<Type> m_jobTypes = new List<Type>();

        public JobManager()
        {
            

            EventHandlers["commandEntered"] += new Action<string>(cmd =>
            {
                /*if (cmd.ToLower().StartsWith("doJob"))
                {
                    InitializeJob();
                }*/
            });
        }
    }
}
