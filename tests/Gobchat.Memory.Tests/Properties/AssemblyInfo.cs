using System.Runtime.Versioning;

// GenerateAssemblyInfo=false (root Directory.Build.props) suppresses the SDK's auto-generated
// [assembly: SupportedOSPlatform("Windows7.0")]. Without it, this windows-only test assembly's
// call sites count as "reachable on all platforms", so CA1416 flags every call into the
// production assembly (which carries that attribute). Restore it manually (windows7.0 = the
// net10.0-windows default floor), matching the production AssemblyInfo files.
[assembly: SupportedOSPlatform("windows7.0")]
