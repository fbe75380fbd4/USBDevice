using System;
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
            ParameterSetName = "Default",
            HelpMessage = "The specific port index to check.")]
        [Parameter(ParameterSetName = "ByHubName")]
        [Parameter(ParameterSetName = "ByHubInstanceId")]
        public int? PortIndex { get; set; }

        [Parameter(
            ParameterSetName = "Default",
            HelpMessage = "Filter by connection status.")]
        [Parameter(ParameterSetName = "ByHubName")]
        [Parameter(ParameterSetName = "ByHubInstanceId")]
        public SwitchParameter IsConnected { get; set; }

        private readonly IUSBInspector _usbInspector;

        public GetUSBPortCommand()
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
                default:
                    rawPatterns = Array.Empty<string>();
                    break;
            };

            var wildcardPatterns = Array.ConvertAll(rawPatterns, pattern =>
                new WildcardPattern(pattern, WildcardOptions.IgnoreCase | WildcardOptions.Compiled)
            );

            var ports = _usbInspector.GetUSBPortInfo();

            foreach (var port in ports)
            {
                bool matchResult = false;

                switch (activeSet)
                {
                    case "ByHubName":
                        matchResult = MatchesAnyWildcard(port.HubName, wildcardPatterns);
                        break;
                    case "ByHubInstanceId":
                        matchResult = MatchesAnyWildcard(port.HubInstanceId, wildcardPatterns);
                        break;
                    default:
                        matchResult = true;
                        break;
                }

                bool matchesText = wildcardPatterns.Length == 0 || matchResult;
                bool matchesPort = !PortIndex.HasValue || port.PortIndex == PortIndex.Value;
                bool matchesConnection = !IsConnected.IsPresent || port.IsConnected == IsConnected.ToBool();

                if (matchesText && matchesPort && matchesConnection)
                {
                    WriteObject(port);
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
