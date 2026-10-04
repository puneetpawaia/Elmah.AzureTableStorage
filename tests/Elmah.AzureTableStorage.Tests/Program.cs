using System;
using System.Collections.Generic;
using NUnitLite;

namespace Elmah.AzureTableStorage.Tests
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            var trackers = new List<EventHandler>();
            if (Type.GetType("Mono.Runtime") != null)
            {
                // Mono can fail while Coverlet formats the ProcessExit sender.
                // Use Coverlet's in-process registry to flush actual recorded hits
                // before shutdown. An uninstrumented run leaves this list empty.
                AppDomain.CurrentDomain.SetData("Coverlet_RegisteredModuleTrackers", trackers);
            }
            try
            {
                return new AutoRun(typeof(Program).Assembly).Execute(args);
            }
            finally
            {
                foreach (var flush in trackers)
                    flush(null, EventArgs.Empty);
            }
        }
    }
}
