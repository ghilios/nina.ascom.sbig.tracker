namespace ASCOM.NINA.SBIGTracker {
    partial class SetupDialogForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this.cmdOK = new System.Windows.Forms.Button();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.picASCOM = new System.Windows.Forms.PictureBox();
            this.chkTrace = new System.Windows.Forms.CheckBox();
            this.serverPipeNameTextBox = new System.Windows.Forms.TextBox();
            this.serverPipeNameLabel = new System.Windows.Forms.Label();
            this.rpcTimeoutTextBox = new System.Windows.Forms.TextBox();
            this.rpcTimeoutLabel = new System.Windows.Forms.Label();
            this.rpcTimeoutUnitLabel = new System.Windows.Forms.Label();
            this.validationErrorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.picASCOM)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.validationErrorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.cmdOK.Location = new System.Drawing.Point(453, 112);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(59, 24);
            this.cmdOK.TabIndex = 0;
            this.cmdOK.Text = "OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(453, 142);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(59, 25);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // picASCOM
            // 
            this.picASCOM.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.picASCOM.Cursor = System.Windows.Forms.Cursors.Hand;
            this.picASCOM.Image = global::ASCOM.NINA.SBIGTracker.Properties.Resources.ASCOM;
            this.picASCOM.Location = new System.Drawing.Point(464, 9);
            this.picASCOM.Name = "picASCOM";
            this.picASCOM.Size = new System.Drawing.Size(48, 56);
            this.picASCOM.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.picASCOM.TabIndex = 3;
            this.picASCOM.TabStop = false;
            this.picASCOM.Click += new System.EventHandler(this.BrowseToAscom);
            this.picASCOM.DoubleClick += new System.EventHandler(this.BrowseToAscom);
            // 
            // chkTrace
            // 
            this.chkTrace.AutoSize = true;
            this.chkTrace.Location = new System.Drawing.Point(295, 119);
            this.chkTrace.Name = "chkTrace";
            this.chkTrace.Size = new System.Drawing.Size(69, 17);
            this.chkTrace.TabIndex = 6;
            this.chkTrace.Text = "Trace on";
            this.chkTrace.UseVisualStyleBackColor = true;
            // 
            // serverPipeNameTextBox
            // 
            this.serverPipeNameTextBox.Location = new System.Drawing.Point(95, 44);
            this.serverPipeNameTextBox.Name = "serverPipeNameTextBox";
            this.serverPipeNameTextBox.Size = new System.Drawing.Size(269, 20);
            this.serverPipeNameTextBox.TabIndex = 8;
            this.serverPipeNameTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // serverPipeNameLabel
            // 
            this.serverPipeNameLabel.AutoSize = true;
            this.serverPipeNameLabel.Location = new System.Drawing.Point(13, 47);
            this.serverPipeNameLabel.Name = "serverPipeNameLabel";
            this.serverPipeNameLabel.Size = new System.Drawing.Size(59, 13);
            this.serverPipeNameLabel.TabIndex = 9;
            this.serverPipeNameLabel.Text = "Pipe Name";
            // 
            // rpcTimeoutTextBox
            // 
            this.rpcTimeoutTextBox.Location = new System.Drawing.Point(95, 81);
            this.rpcTimeoutTextBox.Name = "rpcTimeoutTextBox";
            this.rpcTimeoutTextBox.Size = new System.Drawing.Size(269, 20);
            this.rpcTimeoutTextBox.TabIndex = 10;
            this.rpcTimeoutTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // rpcTimeoutLabel
            // 
            this.rpcTimeoutLabel.AutoSize = true;
            this.rpcTimeoutLabel.Location = new System.Drawing.Point(13, 84);
            this.rpcTimeoutLabel.Name = "rpcTimeoutLabel";
            this.rpcTimeoutLabel.Size = new System.Drawing.Size(70, 13);
            this.rpcTimeoutLabel.TabIndex = 11;
            this.rpcTimeoutLabel.Text = "RPC Timeout";
            // 
            // rpcTimeoutUnitLabel
            // 
            this.rpcTimeoutUnitLabel.AutoSize = true;
            this.rpcTimeoutUnitLabel.Location = new System.Drawing.Point(377, 84);
            this.rpcTimeoutUnitLabel.Name = "rpcTimeoutUnitLabel";
            this.rpcTimeoutUnitLabel.Size = new System.Drawing.Size(47, 13);
            this.rpcTimeoutUnitLabel.TabIndex = 12;
            this.rpcTimeoutUnitLabel.Text = "seconds";
            // 
            // validationErrorProvider
            // 
            this.validationErrorProvider.ContainerControl = this;
            // 
            // SetupDialogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(522, 175);
            this.Controls.Add(this.rpcTimeoutUnitLabel);
            this.Controls.Add(this.rpcTimeoutLabel);
            this.Controls.Add(this.rpcTimeoutTextBox);
            this.Controls.Add(this.serverPipeNameLabel);
            this.Controls.Add(this.serverPipeNameTextBox);
            this.Controls.Add(this.chkTrace);
            this.Controls.Add(this.picASCOM);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SetupDialogForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NINA Legacy SBIG Tracker Setup";
            ((System.ComponentModel.ISupportInitialize)(this.picASCOM)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.validationErrorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.PictureBox picASCOM;
        private System.Windows.Forms.CheckBox chkTrace;
        private System.Windows.Forms.TextBox serverPipeNameTextBox;
        private System.Windows.Forms.Label serverPipeNameLabel;
        private System.Windows.Forms.TextBox rpcTimeoutTextBox;
        private System.Windows.Forms.Label rpcTimeoutLabel;
        private System.Windows.Forms.Label rpcTimeoutUnitLabel;
        private System.Windows.Forms.ErrorProvider validationErrorProvider;
    }
}