using SilverSim.Updater;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SilverSim.Main.GUI
{
    public class GuiApplication : Form
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.Run(new GuiApplication(args));
        }

        NotifyIcon m_TrayIcon;
        ContextMenu m_TrayMenu;
        object m_Startup;
        readonly string[] m_Args;

        public GuiApplication(string[] args)
        {
            m_Args = args;
            m_TrayMenu = new ContextMenu();
            m_TrayMenu.MenuItems.Add("Exit", OnExit);

            m_TrayIcon = new NotifyIcon();
            m_TrayIcon.Text = "SilverSim";
            m_TrayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);
            m_TrayIcon.ContextMenu = m_TrayMenu;
            m_TrayIcon.Visible = true;


            CoreUpdater.Instance.OnUpdateLog += OnCoreUpdaterLog;
            if (!args.Contains("--no-auto-update"))
            {
                CoreUpdater.Instance.CheckForUpdates();
            }
            CoreUpdater.Instance.VerifyInstallation();

            if (args.Contains("--update-only"))
            {
                return;
            }

            if (CoreUpdater.Instance.IsRestartRequired)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location);
                StringBuilder outarg = new StringBuilder();
                foreach (string arg in args)
                {
                    outarg.AppendFormat("\"{0}\" ", arg);
                }
                Process.Start(Assembly.GetExecutingAssembly().Location, outarg.ToString());
                Application.Exit();
                return;
            }

            new Thread(BackgroundThread).Start();
        }

        void BackgroundThread()
        {
            /* by not hard referencing the assembly we can actually implement an updater concept here */
            Assembly assembly = Assembly.Load("SilverSim.Main.Common");
            Type t = assembly.GetType("SilverSim.Main.Common.Startup");
            m_Startup = Activator.CreateInstance(t);
            PropertyInfo pi = t.GetProperty("IsRunningAsService");
            pi.SetMethod.Invoke(m_Startup, new object[] { true });
            MethodInfo mi = t.GetMethod("Run");
            Action<string> del = OnConsoleWrite;
            if (!(bool)mi.Invoke(m_Startup, new object[] { m_Args, del }))
            {
                Thread.Sleep(3000);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            base.OnLoad(e);
        }

        void OnConsoleWrite(string msg)
        {
            m_TrayIcon.BalloonTipTitle = "SilverSim";
            m_TrayIcon.BalloonTipText = msg;
            m_TrayIcon.ShowBalloonTip(1000);
        }

        void OnCoreUpdaterLog(CoreUpdater.LogType type, string message)
        {
            Console.WriteLine("Updater - [{0}] - {1}", type.ToString(), message);
        }



        void OnExit(object sender, EventArgs e)
        {
            if (null != m_Startup)
            {
                Type t = m_Startup.GetType();
                MethodInfo mi = t.GetMethod("Shutdown");
                mi.Invoke(m_Startup, new object[0]);
            }
            Application.Exit();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                m_TrayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
