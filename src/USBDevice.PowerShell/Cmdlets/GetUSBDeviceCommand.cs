using System;
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
        [Parameter(
            ParameterSetName = "ByHubName",
            Mandatory = true,
            HelpMessage = "The name of the USB hub (supports wildcards like *).")]
        public string[] HubName { get; set; }

        [Parameter(
            ParameterSetName = "ByHubInstanceId",
            Mandatory = true,
            HelpMessage = "The unique instance ID of the USB hub (supports wildcards).")]
        public string[] HubInstanceId { get; set; }

        [Parameter(
            ParameterSetName = "ByFriendlyName",
            Mandatory = true,
            HelpMessage = "The friendly display name of the USB device (supports wildcards).")]
        public string[] FriendlyName { get; set; }

        [Parameter(
            ParameterSetName = "ByUsbVersion",
            Mandatory = true,
            HelpMessage = "The USB version standard (supports wildcards, e.g., 3.*).")]
        public string[] UsbVersion { get; set; }

        [Parameter(
            ParameterSetName = "Default",
            HelpMessage = "The specific port index to check.")]
        [Parameter(ParameterSetName = "ByHubName")]
        [Parameter(ParameterSetName = "ByHubInstanceId")]
        [Parameter(ParameterSetName = "ByFriendlyName")]
        [Parameter(ParameterSetName = "ByUsbVersion")]
        public int? PortIndex { get; set; }

        private readonly IUSBInspector _usbInspector;

        public GetUSBDeviceCommand()
        {
            _usbInspector = new Win32USBInspector();
        }

        protected override void ProcessRecord()
        {
            string activeSet = ParameterSetName;

            string[] rawPatterns;

            switch (activeSet)
            {
                case "ByHubName":
                    rawPatterns = HubName ?? Array.Empty<string>();
                    break;
                case "ByHubInstanceId":
                    rawPatterns = HubInstanceId ?? Array.Empty<string>();
                    break;
                case "ByFriendlyName":
                    rawPatterns = FriendlyName ?? Array.Empty<string>();
                    break;
                case "ByUsbVersion":
                    rawPatterns = UsbVersion ?? Array.Empty<string>();
                    break;
                default:
                    rawPatterns = Array.Empty<string>();
                    break;
            }
            ;

            var wildcardPatterns = Array.ConvertAll(rawPatterns, pattern =>
                new WildcardPattern(pattern, WildcardOptions.IgnoreCase | WildcardOptions.Compiled)
            );

            var devices = _usbInspector.GetUSBDeviceInfo();
            
            foreach (var device in devices)
            {
                bool matchResult = false;

                switch (activeSet)
                {
                    case "ByHubName":
                        matchResult = MatchesAnyWildcard(device.HubName, wildcardPatterns);
                        break;
                    case "ByHubInstanceId":
                        matchResult = MatchesAnyWildcard(device.HubInstanceId, wildcardPatterns);
                        break;
                    case "ByFriendlyName":
                        matchResult = MatchesAnyWildcard(device.FriendlyName, wildcardPatterns);
                        break;
                    case "ByUsbVersion":
                        matchResult = MatchesAnyWildcard(device.USBVersion, wildcardPatterns);
                        break;
                    default:
                        matchResult = true;
                        break;
                }

                bool matchesText = wildcardPatterns.Length == 0 || matchResult;
                bool matchesPort = !PortIndex.HasValue || device.PortIndex == PortIndex.Value;
                
                if (matchesText && matchesPort)
                {
                    WriteObject(device);
                }
            }
        }

        private bool MatchesAnyWildcard(string targetValue, WildcardPattern[] patterns)
        {
            if (string.IsNullOrEmpty(targetValue)) return false;

            foreach (var pattern in patterns)
            {
                if (pattern.IsMatch(targetValue))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
