﻿// SilverSim is distributed under the terms of the
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

using Microsoft.Win32;
using SilverSim.Updater;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
            m_TrayMenu.MenuItems.Add("Show Last Message", OnShowLastBallon);
            m_TrayMenu.MenuItems.Add("Shutdown Instance", OnExit);

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
            SystemEvents.SessionEnded += OnSessionEnded;
            if (!(bool)mi.Invoke(m_Startup, new object[] { m_Args, del }))
            {
                Thread.Sleep(3000);
            }
            SystemEvents.SessionEnded -= OnSessionEnded;
        }

        void OnSessionEnded(object sender, SessionEndedEventArgs e)
        {
            TriggerShutdown();
            Application.Exit();
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
            m_TrayIcon.ShowBalloonTip(10000);
        }

        void OnCoreUpdaterLog(CoreUpdater.LogType type, string message)
        {
            m_TrayIcon.BalloonTipTitle = "SilverSim";
            m_TrayIcon.BalloonTipText = string.Format("Updater: [{0}] - {1}", type.ToString(), message);
            m_TrayIcon.ShowBalloonTip(10000);
        }

        void TriggerShutdown()
        {
            if (null != m_Startup)
            {
                Type t = m_Startup.GetType();
                MethodInfo mi = t.GetMethod("Shutdown");
                mi.Invoke(m_Startup, new object[0]);
            }
        }

        void OnShowLastBallon(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(m_TrayIcon.BalloonTipText))
            {
                m_TrayIcon.ShowBalloonTip(30000);
            }
        }

        void OnExit(object sender, EventArgs e)
        {
            TriggerShutdown();
            Application.Exit();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                m_TrayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}