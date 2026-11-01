using System.Management.Automation;
using System.Management.Automation.Runspaces;
using USBDevice.Domain.Interfaces;
using USBDevice.Domain.Models;
using USBDevice.PowerShell.Cmdlets;

namespace USBDevice.Tests;

public class GetUSBDeviceCommandTests
{
    [Fact]
    public void WithoutFilters_ReturnsAllDevices()
    {
        var results = InvokeGetUSBDevice();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void HubNameFilter_IsCaseInsensitiveAndSupportsWildcards()
    {
        var results = InvokeGetUSBDevice(("HubName", "*root hub*"));

        Assert.Equal(2, results.Count);
        Assert.All(results, device => Assert.Equal("USB Root Hub", device.Properties["HubName"].Value));
    }

    [Fact]
    public void HubInstanceIdFilter_MatchesWildcard()
    {
        var results = InvokeGetUSBDevice(("HubInstanceId", "USB\\ROOT_HUB30*"));

        Assert.Equal(2, results.Count);
        Assert.All(results, device =>
            Assert.Equal("USB\\ROOT_HUB30\\A", device.Properties["HubInstanceId"].Value));
    }

    [Fact]
    public void FriendlyNameFilter_MatchesWildcard()
    {
        var results = InvokeGetUSBDevice(("FriendlyName", "*keyboard*"));

        var device = Assert.Single(results);
        Assert.Equal("USB Keyboard", device.Properties["FriendlyName"].Value);
    }

    [Fact]
    public void UsbVersionFilter_CanBeCombinedWithPortIndex()
    {
        var results = InvokeGetUSBDevice(("UsbVersion", "3.*"), ("PortIndex", 2));

        var device = Assert.Single(results);
        Assert.Equal((uint)2, device.Properties["PortIndex"].Value);
        Assert.Equal("3.0", device.Properties["USBVersion"].Value);
    }

    [Fact]
    public void FriendlyNameFilter_MatchesAnyOfMultiplePatterns()
    {
        var results = InvokeGetUSBDevice(("FriendlyName", new[] { "*keyboard*", "*mouse*" }));

        Assert.Equal(2, results.Count);
        Assert.All(results, device =>
            Assert.Contains(
                (string)device.Properties["FriendlyName"].Value,
                new[] { "USB Keyboard", "USB Mouse" }));
    }

    private static IReadOnlyList<PSObject> InvokeGetUSBDevice(params (string Name, object Value)[] parameters)
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Commands.Add(new SessionStateCmdletEntry(
            "Get-USBDevice",
            typeof(TestGetUSBDeviceCommand),
            null));

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var powershell = System.Management.Automation.PowerShell.Create();
        powershell.Runspace = runspace;
        powershell.AddCommand("Get-USBDevice");

        foreach (var (name, value) in parameters)
        {
            powershell.AddParameter(name, value);
        }

        var results = powershell.Invoke();
        Assert.Empty(powershell.Streams.Error);
        return results;
    }
}

[Cmdlet(VerbsCommon.Get, "USBDevice")]
public sealed class TestGetUSBDeviceCommand : GetUSBDeviceCommand
{
    public TestGetUSBDeviceCommand()
        : base(new StubUSBInspector())
    {
    }

    private sealed class StubUSBInspector : IUSBInspector
    {
        public IEnumerable<USBDeviceInfo> GetUSBDeviceInfo()
        {
            return new[]
            {
                new USBDeviceInfo
                {
                    HubName = "USB Root Hub",
                    HubInstanceId = "USB\\ROOT_HUB30\\A",
                    PortIndex = 1,
                    FriendlyName = "USB Keyboard",
                    USBVersion = "2.0",
                    VendorId = "1234",
                    ProductId = "0001",
                    MaxSupportedSpeed = "SuperSpeed",
                    NegotiatedSpeed = "HighSpeed"
                },
                new USBDeviceInfo
                {
                    HubName = "USB Root Hub",
                    HubInstanceId = "USB\\ROOT_HUB30\\A",
                    PortIndex = 2,
                    FriendlyName = "USB Mouse",
                    USBVersion = "3.0",
                    VendorId = "1234",
                    ProductId = "0002",
                    MaxSupportedSpeed = "SuperSpeedPlus",
                    NegotiatedSpeed = "SuperSpeed"
                },
                new USBDeviceInfo
                {
                    HubName = "External Hub",
                    HubInstanceId = "USB\\VID_1234&PID_5678\\B",
                    PortIndex = 3,
                    FriendlyName = "Webcam",
                    USBVersion = "2.0",
                    VendorId = "5678",
                    ProductId = "0003",
                    MaxSupportedSpeed = "HighSpeed",
                    NegotiatedSpeed = "HighSpeed"
                }
            };
        }

        public IEnumerable<USBPortInfo> GetUSBPortInfo()
        {
            return Array.Empty<USBPortInfo>();
        }
    }
}
