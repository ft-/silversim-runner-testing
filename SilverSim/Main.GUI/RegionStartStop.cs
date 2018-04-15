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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SilverSim.Main.GUI
{
    public partial class RegionStartStop : Form
    {
        private GuiApplication m_App;

        public RegionStartStop(GuiApplication app)
        {
            InitializeComponent();
            m_App = app;
            RegionList.View = View.Details;
            var regionData = (Dictionary<string, string>)m_App.m_GetData?.Invoke("region-id-name-pairs");
            var regionEnabled = (string[])m_App.m_GetData?.Invoke("region-ids/enabled");
            var regionOnline = (string[])m_App.m_GetData?.Invoke("region-ids/online");

            foreach (KeyValuePair<string, string> kvp in regionData)
            {
                ListViewItem item = new ListViewItem
                {
                    Text = kvp.Key
                };
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, kvp.Value));
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, regionOnline.Contains(kvp.Key) ? "Yes" : "No"));
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, regionEnabled.Contains(kvp.Key) ? "Yes" : "No"));
                RegionList.Items.Add(item);
            }
            RegionList.Refresh();
        }

        private void startStopRegionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in RegionList.SelectedItems)
            {
                var regionOnline = (string[])m_App.m_GetData?.Invoke("region-ids/online");
                try
                {
                    if (regionOnline.Contains(item.Text))
                    {
                        m_App.m_ExecuteCommand?.Invoke(new List<string> { "stop", "region", item.SubItems[1].Text });
                    }
                    else
                    {
                        m_App.m_ExecuteCommand?.Invoke(new List<string> { "start", "region", item.SubItems[1].Text });
                    }
                    regionOnline = (string[])m_App.m_GetData?.Invoke("region-ids/online");
                    item.SubItems[2].Text = regionOnline.Contains(item.Text) ? "Yes" : "No";
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    /* ignore */
                }
            }
        }

        private void enableDisableRegionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in RegionList.SelectedItems)
            {
                var regionEnabled = (string[])m_App.m_GetData?.Invoke("region-ids/enabled");
                try
                {
                    if (regionEnabled.Contains(item.Text))
                    {
                        m_App.m_ExecuteCommand?.Invoke(new List<string> { "disable", "region", item.SubItems[1].Text });
                    }
                    else
                    {
                        m_App.m_ExecuteCommand?.Invoke(new List<string> { "enable", "region", item.SubItems[1].Text });
                    }
                    regionEnabled = (string[])m_App.m_GetData?.Invoke("region-ids/enabled");
                    item.SubItems[3].Text = regionEnabled.Contains(item.Text) ? "Yes" : "No";
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
