using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ASCOM.Utilities;
using ASCOM.NINA.SBIGTracker;

namespace ASCOM.NINA.SBIGTracker {
    [ComVisible(false)]					// Form not registered for COM!
    public partial class SetupDialogForm : Form {
        TraceLogger tl; // Holder for a reference to the driver's trace logger

        public SetupDialogForm(TraceLogger tlDriver) {
            InitializeComponent();

            // Save the provided trace logger for use within the setup dialogue
            tl = tlDriver;

            // Initialise current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            tl.Enabled = chkTrace.Checked;
            Camera.rpcTimeoutSeconds = int.Parse(rpcTimeoutTextBox.Text.Trim());
            Camera.serverPipeName = serverPipeNameTextBox.Text.Trim();
        }

        private void cmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try {
                System.Diagnostics.Process.Start("https://ascom-standards.org/");
            } catch (System.ComponentModel.Win32Exception noBrowser) {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            } catch (System.Exception other) {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI() {
            chkTrace.Checked = tl.Enabled;

            serverPipeNameTextBox.Text = Camera.serverPipeName;
            rpcTimeoutTextBox.Text = Camera.rpcTimeoutSeconds.ToString();
            serverPipeNameTextBox.Validating += ServerPipeNameTextBox_Validating;
            rpcTimeoutTextBox.Validating += RpcTimeoutTextBox_Validating;
        }

        private void ServerPipeNameTextBox_Validating(object sender, CancelEventArgs e) {
            if (string.IsNullOrWhiteSpace(serverPipeNameTextBox.Text)) {
                e.Cancel = true;
                validationErrorProvider.SetError(serverPipeNameTextBox, "Input cannot be blank or only whitespace");
            } else {
                e.Cancel = false;
                validationErrorProvider.SetError(serverPipeNameTextBox, "");
            }
        }

        private void RpcTimeoutTextBox_Validating(object sender, CancelEventArgs e) {
            int dummy;
            if (!int.TryParse(rpcTimeoutTextBox.Text.Trim(), out dummy) || dummy <= 0) {
                e.Cancel = true;
                validationErrorProvider.SetError(rpcTimeoutTextBox, "RPC timeout must be a positive integer");
            } else {
                e.Cancel = false;
                validationErrorProvider.SetError(rpcTimeoutTextBox, "");
            }
        }
    }
}