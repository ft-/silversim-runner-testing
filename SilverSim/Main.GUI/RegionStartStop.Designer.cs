namespace SilverSim.Main.GUI
{
    partial class RegionStartStop
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.RegionList = new System.Windows.Forms.ListView();
            this.regionId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.regionName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.regionOnline = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.regionEnabled = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.regionControlContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.startStopRegionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.enableDisableRegionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.regionControlContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // RegionList
            // 
            this.RegionList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.regionId,
            this.regionName,
            this.regionOnline,
            this.regionEnabled});
            this.RegionList.ContextMenuStrip = this.regionControlContextMenuStrip;
            this.RegionList.FullRowSelect = true;
            this.RegionList.Location = new System.Drawing.Point(-1, 2);
            this.RegionList.Name = "RegionList";
            this.RegionList.Size = new System.Drawing.Size(801, 451);
            this.RegionList.TabIndex = 0;
            this.RegionList.UseCompatibleStateImageBehavior = false;
            // 
            // regionId
            // 
            this.regionId.Text = "ID";
            this.regionId.Width = 200;
            // 
            // regionName
            // 
            this.regionName.Text = "Name";
            this.regionName.Width = 200;
            // 
            // regionOnline
            // 
            this.regionOnline.Text = "Online";
            // 
            // regionEnabled
            // 
            this.regionEnabled.Text = "Enabled";
            // 
            // regionControlContextMenuStrip
            // 
            this.regionControlContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startStopRegionToolStripMenuItem,
            this.enableDisableRegionToolStripMenuItem});
            this.regionControlContextMenuStrip.Name = "regionControlContextMenuStrip";
            this.regionControlContextMenuStrip.Size = new System.Drawing.Size(193, 48);
            // 
            // startStopRegionToolStripMenuItem
            // 
            this.startStopRegionToolStripMenuItem.Name = "startStopRegionToolStripMenuItem";
            this.startStopRegionToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.startStopRegionToolStripMenuItem.Text = "Start/Stop Region";
            this.startStopRegionToolStripMenuItem.Click += new System.EventHandler(this.startStopRegionToolStripMenuItem_Click);
            // 
            // enableDisableRegionToolStripMenuItem
            // 
            this.enableDisableRegionToolStripMenuItem.Name = "enableDisableRegionToolStripMenuItem";
            this.enableDisableRegionToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.enableDisableRegionToolStripMenuItem.Text = "Enable/Disable Region";
            this.enableDisableRegionToolStripMenuItem.Click += new System.EventHandler(this.enableDisableRegionToolStripMenuItem_Click);
            // 
            // RegionStartStop
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.RegionList);
            this.Name = "RegionStartStop";
            this.Text = "Region List";
            this.regionControlContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView RegionList;
        private System.Windows.Forms.ColumnHeader regionId;
        private System.Windows.Forms.ColumnHeader regionName;
        private System.Windows.Forms.ColumnHeader regionOnline;
        private System.Windows.Forms.ColumnHeader regionEnabled;
        private System.Windows.Forms.ContextMenuStrip regionControlContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem startStopRegionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem enableDisableRegionToolStripMenuItem;
    }
}