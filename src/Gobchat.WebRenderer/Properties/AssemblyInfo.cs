using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// GenerateAssemblyInfo=false (Directory.Build.props) suppresses the SDK's auto-generated
// [assembly: SupportedOSPlatform("Windows7.0")], so CA1416 treats every WinForms call site as
// "reachable on all platforms". Restore it manually (windows7.0 = the net10.0-windows default floor).
[assembly: SupportedOSPlatform("windows7.0")]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Gobchat.UI")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Gobchat.UI")]
[assembly: AssemblyCopyright("Copyright © 2019-2023 MarbleBag, Copyright © 2026 Shuro")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("9b4f225d-b0a3-400e-92e0-6f4cd53ac8b5")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]