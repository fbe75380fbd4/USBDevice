using System;
using System.Runtime.InteropServices;
using System.Management.Automation;

namespace USBDevice.PowerShell.Initialization
{
    public sealed class USBDeviceModuleInitializer : IModuleAssemblyInitializer
    {
        public void OnImport()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException(
                    "The USBDevice module is supported only on Windows because it relies on Win32 USB APIs.");
            }
        }
    }
}
