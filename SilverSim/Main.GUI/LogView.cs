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

using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SilverSim.Main.GUI
{
    public partial class LogView : Form
    {
        private readonly object m_UpdateLock = new object();
        private readonly Type m_LogControllerType;
        private readonly Action<DateTime, string, string, string> m_AddLogLineDelegate;

        private static readonly Color[] Colors =
        {
            Color.Blue,
            Color.Green,
            Color.Cyan,
            Color.Magenta,
            Color.Yellow
        };

        public LogView(Type logControllerType)
        {
            InitializeComponent();
            FormClosed += ClosingLogView;
            m_LogControllerType = logControllerType;
            MethodInfo mi = m_LogControllerType.GetMethod("AddLogAction", new Type[] { typeof(Action<DateTime, string, string, string>) });
            m_AddLogLineDelegate = AddLogLine;
            mi.Invoke(null, new object[] { m_AddLogLineDelegate });
        }

        private void ClosingLogView(object sender, FormClosedEventArgs e)
        {
            MethodInfo mi = m_LogControllerType.GetMethod("RemoveLogAction", new Type[] { typeof(Action<DateTime, string, string, string>) });
            mi.Invoke(null, new object[] { m_AddLogLineDelegate });
        }

        private void ClearLogToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            lock(m_UpdateLock)
            {
                LogViewTextBox.Clear();
            }
        }

        public void AddLogLine(
            DateTime time,
            string level,
            string logger,
            string message)
        {
            lock (m_UpdateLock)
            {
                LogViewTextBox.SuspendLayout();
                string timeStr = time.ToString("HH:mm:ss");
                try
                { 
                    switch (level.ToLower())
                    {
                        case "error":
                            LogViewTextBox.AppendText(string.Format("{0} - [{1}]: {2}", timeStr, logger, message.Trim()), Color.Red);
                            break;

                        case "warn":
                            LogViewTextBox.AppendText(string.Format("{0} - [{1}]: {2}", timeStr, logger, message.Trim()), Color.Yellow);
                            break;

                        default:
                            LogViewTextBox.AppendText(string.Format("{0} - [", timeStr));
                            LogViewTextBox.AppendText(logger, Colors[Math.Abs(logger.ToUpper().GetHashCode()) % Colors.Length]);
                            LogViewTextBox.AppendText(string.Format("]: {0}", message.Trim()));
                            break;
                    }
                }
                catch (Exception e)
                {
                    LogViewTextBox.AppendText("§ " + e.Message);
                }
                LogViewTextBox.AppendText(Environment.NewLine);
                LogViewTextBox.ResumeLayout();
            }
        }
    }
}
