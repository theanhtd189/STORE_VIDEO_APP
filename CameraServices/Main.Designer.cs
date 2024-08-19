using System;

namespace CameraServices
{

    partial class Main

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



            if (m_lDownHandle >= 0)

            {

                CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle);



            }

            if (m_lUserID >= 0)

            {

                CHCNetSDK.NET_DVR_Logout(m_lUserID);

            }

            if (m_bInitSDK == true)

            {

                CHCNetSDK.NET_DVR_Cleanup();

            }



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
            this.listViewIPChannel = new System.Windows.Forms.ListView();
            this.ColumnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label13 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.dateTimeStart = new System.Windows.Forms.DateTimePicker();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.DownloadProgressBar = new System.Windows.Forms.ProgressBar();
            this.btnDownloadTime = new System.Windows.Forms.Button();
            this.label20 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.dateTimeEnd = new System.Windows.Forms.DateTimePicker();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.timerDownload = new System.Windows.Forms.Timer(this.components);
            this.btn_Exit = new System.Windows.Forms.Button();
            this.btnLogin = new System.Windows.Forms.Button();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // listViewIPChannel
            // 
            this.listViewIPChannel.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ColumnHeader1,
            this.ColumnHeader2});
            this.listViewIPChannel.FullRowSelect = true;
            this.listViewIPChannel.GridLines = true;
            this.listViewIPChannel.HideSelection = false;
            this.listViewIPChannel.Location = new System.Drawing.Point(5, 138);
            this.listViewIPChannel.Margin = new System.Windows.Forms.Padding(4);
            this.listViewIPChannel.MultiSelect = false;
            this.listViewIPChannel.Name = "listViewIPChannel";
            this.listViewIPChannel.Size = new System.Drawing.Size(231, 500);
            this.listViewIPChannel.TabIndex = 32;
            this.listViewIPChannel.UseCompatibleStateImageBehavior = false;
            this.listViewIPChannel.View = System.Windows.Forms.View.Details;
            this.listViewIPChannel.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewIPChannel_ItemSelectionChanged);
            // 
            // ColumnHeader1
            // 
            this.ColumnHeader1.Text = "Channel";
            this.ColumnHeader1.Width = 90;
            // 
            // ColumnHeader2
            // 
            this.ColumnHeader2.Text = "Status";
            this.ColumnHeader2.Width = 90;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(11, 654);
            this.label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(171, 16);
            this.label13.TabIndex = 37;
            this.label13.Text = "* DEMO just for reference！";
            // 
            // groupBox1
            // 
            this.groupBox1.Location = new System.Drawing.Point(7, 638);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox1.Size = new System.Drawing.Size(231, 160);
            this.groupBox1.TabIndex = 39;
            this.groupBox1.TabStop = false;
            // 
            // dateTimeStart
            // 
            this.dateTimeStart.CustomFormat = "yyyy-MM-dd HH:mm:ss";
            this.dateTimeStart.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimeStart.Location = new System.Drawing.Point(135, 34);
            this.dateTimeStart.Margin = new System.Windows.Forms.Padding(4);
            this.dateTimeStart.Name = "dateTimeStart";
            this.dateTimeStart.Size = new System.Drawing.Size(209, 22);
            this.dateTimeStart.TabIndex = 42;
            this.dateTimeStart.UseWaitCursor = true;
            this.dateTimeStart.Value = DateTime.Now;           
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.btnLogin);
            this.groupBox4.Controls.Add(this.DownloadProgressBar);
            this.groupBox4.Controls.Add(this.btnDownloadTime);
            this.groupBox4.Controls.Add(this.label20);
            this.groupBox4.Controls.Add(this.label17);
            this.groupBox4.Controls.Add(this.label16);
            this.groupBox4.Controls.Add(this.dateTimeEnd);
            this.groupBox4.Controls.Add(this.dateTimeStart);
            this.groupBox4.Controls.Add(this.groupBox7);
            this.groupBox4.Location = new System.Drawing.Point(840, -2);
            this.groupBox4.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox4.Size = new System.Drawing.Size(395, 798);
            this.groupBox4.TabIndex = 42;
            this.groupBox4.TabStop = false;
            // 
            // DownloadProgressBar
            // 
            this.DownloadProgressBar.Location = new System.Drawing.Point(5, 213);
            this.DownloadProgressBar.Margin = new System.Windows.Forms.Padding(4);
            this.DownloadProgressBar.Name = "DownloadProgressBar";
            this.DownloadProgressBar.Size = new System.Drawing.Size(377, 14);
            this.DownloadProgressBar.TabIndex = 61;
            // 
            // btnDownloadTime
            // 
            this.btnDownloadTime.Location = new System.Drawing.Point(135, 166);
            this.btnDownloadTime.Margin = new System.Windows.Forms.Padding(4);
            this.btnDownloadTime.Name = "btnDownloadTime";
            this.btnDownloadTime.Size = new System.Drawing.Size(209, 38);
            this.btnDownloadTime.TabIndex = 58;
            this.btnDownloadTime.Text = "Download By Time";
            this.btnDownloadTime.UseVisualStyleBackColor = true;
            this.btnDownloadTime.Click += new System.EventHandler(this.btnDownloadTime_Click);
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(132, 134);
            this.label20.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(175, 16);
            this.label20.TabIndex = 56;
            this.label20.Text = "Playback/Download by time";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(9, 78);
            this.label17.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(77, 16);
            this.label17.TabIndex = 47;
            this.label17.Text = "Ending time";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(9, 26);
            this.label16.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(95, 16);
            this.label16.TabIndex = 45;
            this.label16.Text = "Beginning time";
            // 
            // dateTimeEnd
            // 
            this.dateTimeEnd.CustomFormat = "yyyy-MM-dd HH:mm:ss";
            this.dateTimeEnd.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimeEnd.Location = new System.Drawing.Point(135, 90);
            this.dateTimeEnd.Margin = new System.Windows.Forms.Padding(4);
            this.dateTimeEnd.Name = "dateTimeEnd";
            this.dateTimeEnd.Size = new System.Drawing.Size(207, 22);
            this.dateTimeEnd.TabIndex = 43;
            this.dateTimeEnd.Value = DateTime.Now;
            // 
            // groupBox7
            // 
            this.groupBox7.Location = new System.Drawing.Point(5, 132);
            this.groupBox7.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox7.Size = new System.Drawing.Size(386, 14);
            this.groupBox7.TabIndex = 46;
            this.groupBox7.TabStop = false;
            // 
            // timerDownload
            // 
            this.timerDownload.Tick += new System.EventHandler(this.timerProgress_Tick);
            // 
            // btn_Exit
            // 
            this.btn_Exit.Location = new System.Drawing.Point(1053, 806);
            this.btn_Exit.Margin = new System.Windows.Forms.Padding(4);
            this.btn_Exit.Name = "btn_Exit";
            this.btn_Exit.Size = new System.Drawing.Size(169, 40);
            this.btn_Exit.TabIndex = 44;
            this.btn_Exit.Text = "Exit";
            this.btn_Exit.UseVisualStyleBackColor = true;
            this.btn_Exit.Click += new System.EventHandler(this.btn_Exit_Click);
            // 
            // btnLogin
            // 
            this.btnLogin.Location = new System.Drawing.Point(5, 235);
            this.btnLogin.Margin = new System.Windows.Forms.Padding(4);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(209, 38);
            this.btnLogin.TabIndex = 62;
            this.btnLogin.Text = "Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1239, 844);
            this.Controls.Add(this.btn_Exit);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.listViewIPChannel);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox4);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MainWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Main Window";
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }



        #endregion



        private System.Windows.Forms.ListView listViewIPChannel;

        private System.Windows.Forms.ColumnHeader ColumnHeader1;

        private System.Windows.Forms.ColumnHeader ColumnHeader2;

        private System.Windows.Forms.Label label13;

        private System.Windows.Forms.GroupBox groupBox1;

        private System.Windows.Forms.DateTimePicker dateTimeStart;

        private System.Windows.Forms.GroupBox groupBox4;

        private System.Windows.Forms.DateTimePicker dateTimeEnd;

        private System.Windows.Forms.Label label17;

        private System.Windows.Forms.Label label16;

        private System.Windows.Forms.GroupBox groupBox7;

        private System.Windows.Forms.Label label20;

        private System.Windows.Forms.Button btnDownloadTime;

        private System.Windows.Forms.ProgressBar DownloadProgressBar;

        private System.Windows.Forms.Timer timerDownload;

        private System.Windows.Forms.Button btn_Exit;
        private System.Windows.Forms.Button btnLogin;
    }

}



