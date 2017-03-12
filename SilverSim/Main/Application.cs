// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Updater;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SilverSim.Main
{
    static class Application
    {
        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        static void Main(string[] args)
        {
            Thread.CurrentThread.Name = "SilverSim:Main";
            CoreUpdater.Instance.CheckForUpdates();
            CoreUpdater.Instance.VerifyInstallation();

            if(CoreUpdater.Instance.IsRestartRequired)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location);
                StringBuilder outarg = new StringBuilder();
                foreach(string arg in args)
                {
                    outarg.AppendFormat("\"{0}\" ", arg);
                }
                Process.Start(Assembly.GetExecutingAssembly().Location, outarg.ToString());
                return;
            }

            /* by not hard referencing the assembly we can actually implement an updater concept here */
            Assembly assembly = Assembly.Load("SilverSim.Main.Common");
            Type t = assembly.GetType("SilverSim.Main.Common.Startup");
            object startup = Activator.CreateInstance(t);
            MethodInfo mi = t.GetMethod("Run");
            Action<string> del = Console.WriteLine;
            if(!(bool)mi.Invoke(startup, new object[] { args, del }))
            {
                Environment.Exit(1);
            }
        }
    }
}
