using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// Expose internal types (e.g. Module/*/Internal, Core/Config internals) to the unit test project.
[assembly: InternalsVisibleTo("Gobchat.App.Tests")]

// GenerateAssemblyInfo=false (Directory.Build.props) suppresses the SDK's auto-generated
// [assembly: SupportedOSPlatform("Windows7.0")], so CA1416 treats every WinForms call site as
// "reachable on all platforms". Restore it manually (windows7.0 = the net10.0-windows default floor).
[assembly: SupportedOSPlatform("windows7.0")]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("GobchatEx")]
[assembly: AssemblyDescription("A FFXIV chat overlay")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("GobchatEx")]
[assembly: AssemblyCopyright("Copyright © 2019-2025 MarbleBag, Copyright © 2026 Shuro")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("c91493a8-ee07-48ca-ad62-e924696e4a3f")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Pre-Release (Used for betas)
//
[assembly: AssemblyVersion("2.0.1.0")]
[assembly: AssemblyFileVersion("2.0.1.0")]
