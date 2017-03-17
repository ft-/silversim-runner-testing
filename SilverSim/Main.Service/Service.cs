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
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace SilverSim.Main.Service
{
    sealed class MainService : ServiceBase
    {
        public const string SERVICE_NAME = "SilverSim";
        Action m_ShutdownDelegate;

        public MainService()
        {
            ServiceName = SERVICE_NAME;
            EventLog eventLog = EventLog;
            eventLog.Source = SERVICE_NAME;
            eventLog.Log = "Application";

            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = true;
            CanStop = true;

            if (!EventLog.SourceExists(eventLog.Source))
            {
                EventLog.CreateEventSource(eventLog.Source, eventLog.Log);
            }
        }

        [SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule")]
        static void Main()
        {
            Run(new MainService());
        }

        protected override void OnStart(string[] args)
        {
            new Thread(ServiceMain).Start(args);
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            Action shutdownDelegate = m_ShutdownDelegate;
            if(shutdownDelegate != null)
            {
                shutdownDelegate();
            }
            while(!m_ShutdownCompleteEvent.WaitOne(1000))
            {
                RequestAdditionalTime(1000);
            }
            base.OnStop();
        }

        protected override void OnShutdown()
        {
            Action shutdownDelegate = m_ShutdownDelegate;
            if (shutdownDelegate != null)
            {
                shutdownDelegate();
            }
            while (!m_ShutdownCompleteEvent.WaitOne(1000))
            {
                RequestAdditionalTime(1000);
            }
            base.OnShutdown();
        }

        readonly ManualResetEvent m_ShutdownCompleteEvent = new ManualResetEvent(false);

        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        void ServiceMain(object obj)
        {
            string[] args = (string[])obj;
            EventLog eventLog = EventLog;

            m_ShutdownCompleteEvent.Reset();

            Thread.CurrentThread.Name = "SilverSim:Main";

            try
            {
                CoreUpdater.Instance.CheckForUpdates();
                CoreUpdater.Instance.VerifyInstallation();
            }
            catch
            {
                Stop();
                m_ShutdownCompleteEvent.Set();
                return;
            }

            if(CoreUpdater.Instance.IsRestartRequired)
            {
                Stop();
                m_ShutdownCompleteEvent.Set();
                return;
            }

            /* by not hard referencing the assembly we can actually implement an updater concept here */
            Assembly assembly = Assembly.Load("SilverSim.Main.Common");
            Type t = assembly.GetType("SilverSim.Main.Common.Startup");
            object startup = Activator.CreateInstance(t);
            MethodInfo mi = t.GetMethod("Run");
            PropertyInfo pi = t.GetProperty("IsRunningAsService");
            pi.SetMethod.Invoke(startup, new object[] { true });
            m_ShutdownDelegate = (Action)Delegate.CreateDelegate(typeof(Action), startup, t.GetMethod("Shutdown"));
            Action<string> del = eventLog.WriteEntry;
            if (!(bool)mi.Invoke(startup, new object[] { args, del }))
            {
                Stop();
            }

            m_ShutdownCompleteEvent.Set();
        }

        ~MainService()
        {
            m_ShutdownCompleteEvent.Dispose();
        }
    }
}
