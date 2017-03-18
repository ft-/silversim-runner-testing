// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

using SilverSim.Updater;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SilverSim.Main
{
    static class Application
    {
        static void ConsoleUpdaterLog(CoreUpdater.LogType type, string message)
        {
            Console.WriteLine("Updater - [{0}] - {1}", type.ToString(), message);
        }

        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        static void Main(string[] args)
        {
            Thread.CurrentThread.Name = "SilverSim:Main";
            CoreUpdater.Instance.OnUpdateLog += ConsoleUpdaterLog;
            if (!args.Contains("--no-installed-verify") || args.Contains("--update-only"))
            {
                CoreUpdater.Instance.CheckForUpdates();
                CoreUpdater.Instance.VerifyInstallation();

                if (args.Contains("--update-only"))
                {
                    return;
                }
            }

            if (CoreUpdater.Instance.IsRestartRequired)
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

            CoreUpdater.Instance.OnUpdateLog -= ConsoleUpdaterLog;

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
