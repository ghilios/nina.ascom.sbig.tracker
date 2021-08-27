//tabs=4
// --------------------------------------------------------------------------------
// ASCOM Camera driver for NINA's SBIGTracker
//
// Description:	A thin ASCOM driver that connects to an SBIG tracking CCD that is
//              internal to an SBIG camera connected in NINA using its native driver
//
// Implements:	ASCOM Camera interface version: V3
// Author:		(ghilios) George Hilios <ghilios@gmail.com>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 23-08-2021	ghilios	6.0.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using System.Globalization;
using System.Collections;
using GrpcDotNetNamedPipes;
using NINA.Core.API.ASCOM.Camera;
using System.Threading;

namespace ASCOM.NINA.SBIGTracker {
    //
    // Your driver's DeviceID is ASCOM.NINA.SBIGTracker.Camera
    //
    // The Guid attribute sets the CLSID for ASCOM.SBIGTracker.Camera
    // The ClassInterface/None attribute prevents an empty interface called
    // _SBIGTracker from being created and used as the [default] interface
    //
    // TODO Replace the not implemented exceptions with code to implement the function or
    // throw the appropriate ASCOM exception.
    //

    /// <summary>
    /// ASCOM Camera Driver for SBIGTracker.
    /// </summary>
    [Guid("7754cd37-57ec-4b4e-8371-b692f038c84e")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Camera : ICameraV3 {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        internal static string driverID = "ASCOM.NINA.SBIGTracker.Camera";
        /// <summary>
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
        private static string driverDescription = "ASCOM Camera Driver for NINA.SBIG.Tracker";

        private static string serverPipeNameProfileName = "NINA.ASCOM.Camera.SBIG.Tracker Pipe Name";
        private static string serverPipeNameDefault = "NINA.ASCOM.Camera.SBIG.Tracker";
        private static string rpcTimeoutSecondsProfileName = "NINA.ASCOM.Camera.SBIG.Tracker RPC Timeout Seconds";
        private static string rpcTimeoutSecondsDefault = "10";
        private static string traceStateProfileName = "Trace Level";
        private static readonly string traceStateDefault = "false";
        private NamedPipeChannel rpcChannel;
        private CameraService.CameraServiceClient rpcClient;

        internal static string serverPipeName;
        internal static int rpcTimeoutSeconds;

        private static global::Google.Protobuf.WellKnownTypes.Empty EMPTY_ARGS = new global::Google.Protobuf.WellKnownTypes.Empty();

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Private variable to hold an ASCOM Utilities object
        /// </summary>
        private Util utilities;

        /// <summary>
        /// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
        /// </summary>
        private AstroUtils astroUtilities;

        /// <summary>
        /// Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        internal TraceLogger tl;

        private CancellationTokenSource cts;

        /// <summary>
        /// Initializes a new instance of the <see cref="SBIGTracker"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Camera() {
            tl = new TraceLogger("", "SBIGTracker");
            ReadProfile(); // Read device configuration from the ASCOM Profile store

            tl.LogMessage("Camera", "Starting initialisation");

            connectedState = false; // Initialise connected to false
            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro-utilities object
            cts = new CancellationTokenSource();

            tl.LogMessage("Camera", "Completed initialisation");
        }

        public DateTime? GetDeadline() {
            return DateTime.UtcNow + TimeSpan.FromSeconds(rpcTimeoutSeconds);
        }


        //
        // PUBLIC COM INTERFACE ICameraV3 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog() {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected) {
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");
            }

            using (SetupDialogForm F = new SetupDialogForm(tl)) {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK) {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        public ArrayList SupportedActions {
            get {
                tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters) {
            LogMessage("", "Action {0}, parameters {1} not implemented", actionName, actionParameters);
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw) {
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw) {
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw) {
            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose() {
            // Clean up the trace logger and util objects
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
            rpcClient = null;
            rpcChannel = null;
        }

        public bool Connected {
            get {
                LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set {
                tl.LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected) {
                    return;
                }

                if (value) {
                    Connect();
                } else {
                    Disconnect();
                }
            }
        }

        private System.Timers.Timer heartbeatTimer = null;
        private void Connect() {
            connectedState = true;
            LogMessage("Connected Set", "Connecting to pipe {0}", serverPipeName);
            rpcChannel = new NamedPipeChannel(".", serverPipeName);
            rpcClient = GrpcClientErrorHandlingProxy<CameraService.CameraServiceClient>.Wrap(new CameraService.CameraServiceClient(rpcChannel));
            heartbeatTimer?.Dispose();
            heartbeatTimer = null;

            // TODO: Consider making this heartbeat interval configurable
            heartbeatTimer = new System.Timers.Timer() {
                Interval = 5000,
                AutoReset = true
            };
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            heartbeatTimer.Start();
        }

        private void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            try {
                rpcClient?.CameraXSize_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
            } catch (Exception) {
                tl.LogMessage("IsConnected", "RPC heartbeat failed. Assuming server disconnected");
                Disconnect();
            }
        }

        private void Disconnect() {
            connectedState = false;
            heartbeatTimer?.Stop();
            heartbeatTimer?.Dispose();
            heartbeatTimer = null;
            var oldCts = cts;
            cts = new CancellationTokenSource();
            oldCts?.Cancel();
            oldCts?.Dispose();
            LogMessage("Connected Set", "Disconnecting from pipe {0}", serverPipeName);
        }

        public string Description {
            get {
                tl.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public string DriverInfo {
            get {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = "ASCOM driver for SBIG Tracker CCDs connected in NINA. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion {
            get {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion {
            // set by the driver wizard
            get {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        public string Name {
            get {
                string name = "NINA Legacy SBIG Tracker CCD";
                tl.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ICamera Implementation

        public void AbortExposure() {
            CheckConnected("Not connected");
            rpcClient.AbortExposure(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
        }

        public short BayerOffsetX {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.BayerOffsetX_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token).Value;
                tl.LogMessage("BayerOffsetX Get", value.ToString());
                return checked((short)value);
            }
        }

        public short BayerOffsetY {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.BayerOffsetY_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token).Value;
                tl.LogMessage("BayerOffsetY Get", value.ToString());
                return checked((short)value);
            }
        }

        public short BinX {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.BinX_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token).Value;
                tl.LogMessage("BinX Get", value.ToString());
                return checked((short)value);
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("BinX Set", value.ToString());
                rpcClient.BinX_set(new SetShortPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public short BinY {
            get {
                if (!IsConnected) {
                    return -1;
                }
                var value = rpcClient.BinY_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token).Value;
                tl.LogMessage("BinY Get", value.ToString());
                return checked((short)value);
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("BinY Set", value.ToString());
                rpcClient.BinY_set(new SetShortPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public double CCDTemperature {
            get {
                if (!IsConnected) {
                    return -1;
                }
                return rpcClient.CCDTemperature_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token).Value;
            }
        }

        public DeviceInterface.CameraStates CameraState {
            get {
                if (!IsConnected) {
                    return DeviceInterface.CameraStates.cameraError;
                }

                var value = rpcClient.CameraState_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CameraState Get", value.Value.ToString());
                return (DeviceInterface.CameraStates)value.Value;
            }
        }

        public int CameraXSize {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.CameraXSize_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CameraXSize Get", value.Value.ToString());
                return value.Value;
            }
        }

        public int CameraYSize {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.CameraYSize_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CameraYSize Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanAbortExposure {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanAbortExposure_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanAbortExposure Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanAsymmetricBin {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanAsymmetricBin_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanAsymmetricBin Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanFastReadout {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanFastReadout_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanFastReadout Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanGetCoolerPower {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanGetCoolerPower_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanGetCoolerPower Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanPulseGuide {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanPulseGuide_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanPulseGuide Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanSetCCDTemperature {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanSetCCDTemperature_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanSetCCDTemperature Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CanStopExposure {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CanStopExposure_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CanStopExposure Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool CoolerOn {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.CoolerOn_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CoolerOn Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("CoolerOn Set", value.ToString());
                rpcClient.CoolerOn_set(new SetBoolPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public double CoolerPower {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.CoolerPower_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("CoolerPower Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double ElectronsPerADU {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.ElectronsPerADU_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ElectronsPerADU Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double ExposureMax {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.ExposureMax_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ExposureMax Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double ExposureMin {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.ExposureMin_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ExposureMin Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double ExposureResolution {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.ExposureResolution_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ExposureResolution Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool FastReadout {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.FastReadout_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("FastReadout Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("FastReadout Set", value.ToString());
                rpcClient.FastReadout_set(new SetBoolPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public double FullWellCapacity {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.FullWellCapacity_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("FullWellCapacity Get", value.Value.ToString());
                return value.Value;
            }
        }

        public short Gain {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.Gain_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("Gain Get", value.Value.ToString());
                return checked((short)value.Value);
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("Gain Set", value.ToString());
                rpcClient.Gain_set(new SetShortPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public short GainMax {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.GainMax_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("GainMax Get", value.Value.ToString());
                return checked((short)value.Value);
            }
        }

        public short GainMin {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.GainMin_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("GainMin Get", value.Value.ToString());
                return checked((short)value.Value);
            }
        }

        public ArrayList Gains {
            get {
                if (!IsConnected) {
                    return new ArrayList();
                }

                var value = rpcClient.Gains_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("Gains Get", value.Value.ToString());
                return new ArrayList(value.Value);
            }
        }

        public bool HasShutter {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.HasShutter_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("HasShutter Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double HeatSinkTemperature {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.HeatSinkTemperature_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("HeatSinkTemperature Get", value.Value.ToString());
                return value.Value;
            }
        }

        public object ImageArray {
            get {
                CheckConnected("Not connected");

                var value = rpcClient.ImageArray_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                var byteData = new byte[value.Data.Length];
                value.Data.CopyTo(byteData, 0);
                var imageData = new ushort[value.Width * value.Height];
                Buffer.BlockCopy(byteData, 0, imageData, 0, value.Data.Length);
                var cameraImageArray = new int[value.Width, value.Height];
                for (int y = 0; y < value.Height; ++y) {
                    for (int x = 0; x < value.Width; ++x) {
                        cameraImageArray[x, y] = imageData[x * value.Height + y];
                    }
                }
                return cameraImageArray;
            }
        }

        public object ImageArrayVariant {
            get {
                CheckConnected("Not connected");

                var value = rpcClient.ImageArray_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                var byteData = new byte[value.Data.Length];
                value.Data.CopyTo(byteData, 0);
                var imageData = new ushort[value.Width * value.Height];
                Buffer.BlockCopy(byteData, 0, imageData, 0, value.Data.Length);
                var cameraImageArrayVariant = new object[value.Width, value.Height];
                for (int y = 0; y < value.Height; ++y) {
                    for (int x = 0; x < value.Width; ++x) {
                        cameraImageArrayVariant[x, y] = imageData[x * value.Height + y];
                    }
                }
                return cameraImageArrayVariant;
            }
        }

        public bool ImageReady {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.ImageReady_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ImageReady Get", value.Value.ToString());
                return value.Value;
            }
        }

        public bool IsPulseGuiding {
            get {
                if (!IsConnected) {
                    return false;
                }

                var value = rpcClient.IsPulseGuiding_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("IsPulseGuiding Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double LastExposureDuration {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.LastExposureDuration_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("LastExposureDuration Get", value.Value.ToString());
                return value.Value;
            }
        }

        public string LastExposureStartTime {
            get {
                if (!IsConnected) {
                    return "";
                }

                var value = rpcClient.LastExposureStartTime_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("LastExposureStartTime Get", value.Value.ToString());
                return value.Value;
            }
        }

        public int MaxADU {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.MaxADU_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("MaxADU Get", value.Value.ToString());
                return value.Value;
            }
        }

        public short MaxBinX {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.MaxBinX_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("MaxBinX Get", value.Value.ToString());
                return checked((short)value.Value);
            }
        }

        public short MaxBinY {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.MaxBinY_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("MaxBinY Get", value.Value.ToString());
                return checked((short)value.Value);
            }
        }

        public int NumX {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.NumX_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("NumX Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("NumX Set", value.ToString());
                rpcClient.NumX_set(new SetIntPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public int NumY {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.NumY_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("NumY Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("NumY Set", value.ToString());
                rpcClient.NumY_set(new SetIntPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public int Offset {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.Offset_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("Offset Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("Offset Set", value.ToString());
                rpcClient.Offset_set(new SetIntPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public int OffsetMax {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.OffsetMax_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("OffsetMax Get", value.Value.ToString());
                return value.Value;
            }
        }

        public int OffsetMin {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.OffsetMin_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("OffsetMin Get", value.Value.ToString());
                return value.Value;
            }
        }

        public ArrayList Offsets {
            get {
                if (!IsConnected) {
                    return new ArrayList();
                }

                var value = rpcClient.Offsets_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("Offsets Get", value.Value.ToString());
                return new ArrayList(value.Value);
            }
        }

        public short PercentCompleted {
            get {
                if (!IsConnected) {
                    return 0;
                }

                var value = rpcClient.PercentCompleted_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("PercentCompleted Get", value.Value.ToString());
                return checked((short)value.Value);
            }
        }

        public double PixelSizeX {
            get {
                if (!IsConnected) {
                    return 0;
                }

                var value = rpcClient.PixelSizeX_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("PixelSizeX Get", value.Value.ToString());
                return value.Value;
            }
        }

        public double PixelSizeY {
            get {
                if (!IsConnected) {
                    return 0;
                }

                var value = rpcClient.PixelSizeY_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("PixelSizeY Get", value.Value.ToString());
                return value.Value;
            }
        }

        public void PulseGuide(GuideDirections Direction, int Duration) {
            throw new ASCOM.MethodNotImplementedException("PulseGuide");
        }

        public short ReadoutMode {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.ReadoutMode_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ReadoutMode Get", value.Value.ToString());
                return checked((short)value.Value);
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("ReadoutMode Set", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("ReadoutMode", true);
            }
        }

        public ArrayList ReadoutModes {
            get {
                if (!IsConnected) {
                    return new ArrayList();
                }

                var value = rpcClient.ReadoutModes_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("ReadoutModes Get", value.Value.ToString());
                return new ArrayList(value.Value);
            }
        }

        public string SensorName {
            get {
                CheckConnected("Not connected");

                var value = rpcClient.SensorName_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("SensorName Get", value.Value.ToString());
                return value.Value;
            }
        }

        public DeviceInterface.SensorType SensorType {
            get {
                CheckConnected("Not connected");

                var value = rpcClient.SensorType_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("SensorType Get", value.Value.ToString());
                return (DeviceInterface.SensorType)value.Value;
            }
        }

        public double SetCCDTemperature {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.SetCCDTemperature_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("SetCCDTemperature Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("SetCCDTemperature Set", value.ToString());
                rpcClient.SetCCDTemperature_set(new SetDoublePropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public void StartExposure(double Duration, bool Light) {
            CheckConnected("Not connected");
            rpcClient.StartExposure(new StartExposureRequest() {
                Duration = Duration,
                Light = Light
            }, deadline: GetDeadline(), cancellationToken: cts.Token);
        }

        public int StartX {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.StartX_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("StartX Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("StartX Set", value.ToString());
                rpcClient.StartX_set(new SetIntPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public int StartY {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.StartY_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("StartY Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("StartY Set", value.ToString());
                rpcClient.StartY_set(new SetIntPropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        public void StopExposure() {
            CheckConnected("Not connected");
            rpcClient.StopExposure(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
        }

        public double SubExposureDuration {
            get {
                if (!IsConnected) {
                    return -1;
                }

                var value = rpcClient.SubExposureDuration_get(EMPTY_ARGS, deadline: GetDeadline(), cancellationToken: cts.Token);
                tl.LogMessage("SubExposureDuration Get", value.Value.ToString());
                return value.Value;
            }
            set {
                CheckConnected("Not connected");
                tl.LogMessage("SubExposureDuration Set", value.ToString());
                rpcClient.SubExposureDuration_set(new SetDoublePropertyRequest() { Value = value }, deadline: GetDeadline(), cancellationToken: cts.Token);
            }
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister) {
            using (var P = new ASCOM.Utilities.Profile()) {
                P.DeviceType = "Camera";
                if (bRegister) {
                    P.Register(driverID, driverDescription);
                } else {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t) {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t) {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected {
            get {
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message) {
            if (!IsConnected) {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile() {
            using (Profile driverProfile = new Profile()) {
                driverProfile.DeviceType = "Camera";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                serverPipeName = driverProfile.GetValue(driverID, serverPipeNameProfileName, string.Empty, serverPipeNameDefault);
                var rpcTimeoutSecondsString = driverProfile.GetValue(driverID, rpcTimeoutSecondsProfileName, string.Empty, rpcTimeoutSecondsDefault);
                if (!int.TryParse(rpcTimeoutSecondsString, out rpcTimeoutSeconds)) {
                    // Deal with profile corruption by reverting to the default
                    rpcTimeoutSeconds = int.Parse(rpcTimeoutSecondsDefault);
                }
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile() {
            using (Profile driverProfile = new Profile()) {
                driverProfile.DeviceType = "Camera";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(driverID, serverPipeNameProfileName, serverPipeName);
                driverProfile.WriteValue(driverID, rpcTimeoutSecondsProfileName, rpcTimeoutSeconds.ToString());
            }
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal void LogMessage(string identifier, string message, params object[] args) {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }
        #endregion
    }
}
