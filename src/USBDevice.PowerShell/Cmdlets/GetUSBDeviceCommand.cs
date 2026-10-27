using System.Management.Automation;
using USBDevice.Domain.Interfaces;
using USBDevice.Domain.Models;
using USBDevice.Infrastructure.Services;

namespace USBDevice.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "USBDevice")]
    [OutputType(typeof(USBDeviceInfo))]
    public class GetUSBDeviceCommand : PSCmdlet
    {
        private readonly IUSBInspector _usbInspector;

        public GetUSBDeviceCommand()
        {
            _usbInspector = new Win32USBInspector();
        }

        protected override void ProcessRecord()
        {
            var devices = _usbInspector.GetUSBDeviceInfo();
            foreach (var device in devices)
            {
                WriteObject(device);
            }
        }
    }
}
