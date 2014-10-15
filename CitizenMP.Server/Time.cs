using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server
{
    public static class Time
    {
        private static long ms_initialCount;

        public static void Initialize()
        {
            ms_initialCount = Stopwatch.GetTimestamp();
        }

        public static long CurrentTime
        {
            get
            {
                return (Stopwatch.GetTimestamp() - ms_initialCount) / 10000;
            }
        }
    }
}
