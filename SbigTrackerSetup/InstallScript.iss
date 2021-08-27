[Setup]
AppID={{6c6d8287-1918-4a91-a8c6-983695b20ddf}
AppName=NINA SBIGTracker ASCOM Camera Driver
AppVerName=NINA SBIGTracker ASCOM Camera Driver 0.2
AppVersion=0.2
AppPublisher=George Hilios <ghilios@gmail.com>
AppPublisherURL=mailto:ghilios@gmail.com
AppSupportURL=https://discord.com/invite/rWRbVbw
AppUpdatesURL=https://github.com/ghilios/nina.ascom.sbig.tracker/releases
VersionInfoVersion=1.0.0
MinVersion=6.1.7601
DefaultDirName="{cf}\ASCOM\Camera"
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir="Output"
OutputBaseFilename="NINA SBIG Tracker ASCOM Driver Setup"
Compression=lzma
SolidCompression=yes
WizardImageFile="..\nina.ascom.sbig.tracker\nina_logo.bmp"
LicenseFile="..\LICENSE"
UninstallFilesDir="{cf}\ASCOM\Uninstall\Camera\ASCOM.NINA.SBIGTracker.Camera"

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{cf}\ASCOM\Uninstall\Camera\ASCOM.NINA.SBIGTracker.Camera"

[Files]
; regserver flag only if native COM, not .NET
; Source: "C:\Users\ghili\src\nina.ascom.sbig.tracker\nina.ascom.sbig.tracker\bin\Release\ASCOM.NINA.SBIGTracker.Camera.dll"; DestDir: "{app}" ;AfterInstall: RegASCOM(); Flags: regserver
Source: "..\nina.ascom.sbig.tracker\bin\Release\ASCOM.NINA.SBIGTracker.Camera.dll"; DestDir: "{app}" ;AfterInstall: RegASCOM()
Source: "..\nina.ascom.sbig.tracker\bin\Release\Castle.Core.AsyncInterceptor.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\Castle.Core.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\Google.Protobuf.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\Grpc.Core.Api.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\GrpcDotNetNamedPipes.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\Newtonsoft.Json.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\System.Buffers.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\System.Memory.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\System.Numerics.Vectors.dll"; DestDir: "{app}"
Source: "..\nina.ascom.sbig.tracker\bin\Release\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{app}"

; Require a read-me HTML to appear after installation, maybe driver's Help doc
Source: "..\nina.ascom.sbig.tracker\bin\Release\Readme.txt"; DestDir: "{app}"; Flags: isreadme

[Run]
Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.NINA.SBIGTracker.Camera.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.NINA.SBIGTracker.Camera.dll"""; Flags: runhidden 64bit; Check: IsWin64

[UninstallRun]
Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.NINA.SBIGTracker.Camera.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.NINA.SBIGTracker.Camera.dll"""; Flags: runhidden 64bit; Check: IsWin64

[Code]
const
   REQUIRED_PLATFORM_VERSION = 6.2;    // Set this to the minimum required ASCOM Platform version for this application

//
// Function to return the ASCOM Platform's version number as a double.
//
function PlatformVersion(): Double;
var
   PlatVerString : String;
begin
   Result := 0.0;  // Initialise the return value in case we can't read the registry
   try
      if RegQueryStringValue(HKEY_LOCAL_MACHINE_32, 'Software\ASCOM','PlatformVersion', PlatVerString) then 
      begin // Successfully read the value from the registry
         Result := StrToFloat(PlatVerString); // Create a double from the X.Y Platform version string
      end;
   except                                                                   
      ShowExceptionMessage;
      Result:= -1.0; // Indicate in the return value that an exception was generated
   end;
end;

//
// Before the installer UI appears, verify that the required ASCOM Platform version is installed.
//
function InitializeSetup(): Boolean;
var
   PlatformVersionNumber : double;
 begin
   Result := FALSE;  // Assume failure
   PlatformVersionNumber := PlatformVersion(); // Get the installed Platform version as a double
   If PlatformVersionNumber >= REQUIRED_PLATFORM_VERSION then	// Check whether we have the minimum required Platform or newer
      Result := TRUE
   else
      if PlatformVersionNumber = 0.0 then
         MsgBox('No ASCOM Platform is installed. Please install Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later from https://www.ascom-standards.org', mbCriticalError, MB_OK)
      else 
         MsgBox('ASCOM Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later is required, but Platform '+ Format('%3.1f', [PlatformVersionNumber]) + ' is installed. Please install the latest Platform before continuing; you will find it at https://www.ascom-standards.org', mbCriticalError, MB_OK);
end;

// Code to enable the installer to uninstall previous versions of itself when a new version is installed
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  UninstallExe: String;
  UninstallRegistry: String;
begin
  if (CurStep = ssInstall) then // Install step has started
	begin
      // Create the correct registry location name, which is based on the AppId
      UninstallRegistry := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}' + '_is1');
      // Check whether an extry exists
      if RegQueryStringValue(HKLM, UninstallRegistry, 'UninstallString', UninstallExe) then
        begin // Entry exists and previous version is installed so run its uninstaller quietly after informing the user
          MsgBox('Setup will now remove the previous version.', mbInformation, MB_OK);
          Exec(RemoveQuotes(UninstallExe), ' /SILENT', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
          sleep(1000);    //Give enough time for the install screen to be repainted before continuing
        end
  end;
end;

//
// Register and unregister the driver with the Chooser
// We already know that the Helper is available
//
procedure RegASCOM();
var
   P: Variant;
begin
   P := CreateOleObject('ASCOM.Utilities.Profile');
   P.DeviceType := 'Camera';
   P.Register('ASCOM.NINA.SBIGTracker.Camera', 'NINA Legacy SBIG Tracker');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
   P: Variant;
begin
   if CurUninstallStep = usUninstall then
   begin
     P := CreateOleObject('ASCOM.Utilities.Profile');
     P.DeviceType := 'Camera';
     P.Unregister('ASCOM.NINA.SBIGTracker.Camera');
  end;
end;
