using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inspector
{
    public static class Runtime
    {
        static Runtime()
        {
            var ThisProcess = System.Diagnostics.Process.GetCurrentProcess(); LastSystemTime = (long)(System.DateTime.Now - ThisProcess.StartTime).TotalMilliseconds; ThisProcess.Dispose();
            StopWatch = new System.Diagnostics.Stopwatch(); StopWatch.Start();
        }
        private static long LastSystemTime;
        private static System.Diagnostics.Stopwatch StopWatch;

        public static long CurrentRuntime { get { return StopWatch.ElapsedMilliseconds + LastSystemTime; } }
    }
}
