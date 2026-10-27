using System.Collections.Generic;
using USBDevice.Domain.Models;

namespace USBDevice.Domain.Interfaces
{
    public interface IUSBInspector
    {
        IEnumerable<USBDeviceInfo> GetUSBDeviceInfo();
        IEnumerable<USBPortInfo> GetUSBPortInfo();
    }
}