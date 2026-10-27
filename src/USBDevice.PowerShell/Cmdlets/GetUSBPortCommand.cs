using System.Management.Automation;
using USBDevice.Domain.Interfaces;
using USBDevice.Domain.Models;
using USBDevice.Infrastructure.Services;

namespace USBDevice.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "USBPort")]
    [OutputType(typeof(USBPortInfo))]
    public class GetUSBPortCommand : PSCmdlet
    {
        private readonly IUSBInspector _usbInspector;

        public GetUSBPortCommand()
        {
            _usbInspector = new Win32USBInspector();
        }

        protected override void ProcessRecord()
        {
            var ports = _usbInspector.GetUSBPortInfo();
            foreach (var port in ports)
            {
                WriteObject(port);
            }
        }
    }
}
