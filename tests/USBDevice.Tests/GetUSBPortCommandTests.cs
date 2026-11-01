using System.Management.Automation;
using System.Management.Automation.Runspaces;
using USBDevice.Domain.Interfaces;
using USBDevice.Domain.Models;
using USBDevice.PowerShell.Cmdlets;

namespace USBDevice.Tests;

public class GetUSBPortCommandTests
{
    [Fact]
    public void WithoutFilters_ReturnsAllPorts()
    {
        var results = InvokeGetUSBPort();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void HubNameFilter_IsCaseInsensitiveAndSupportsWildcards()
    {
        var results = InvokeGetUSBPort(("HubName", "*root hub*"));

        Assert.Equal(2, results.Count);
        Assert.All(results, port => Assert.Equal("USB Root Hub", port.Properties["HubName"].Value));
    }

    [Fact]
    public void HubInstanceIdFilter_MatchesWildcard()
    {
        var results = InvokeGetUSBPort(("HubInstanceId", "USB\\ROOT_HUB30*"));

        Assert.Equal(2, results.Count);
        Assert.All(results, port =>
            Assert.Equal("USB\\ROOT_HUB30\\A", port.Properties["HubInstanceId"].Value));
    }

    [Fact]
    public void PortIndexAndIsConnected_FiltersResultsTogether()
    {
        var results = InvokeGetUSBPort(("PortIndex", 2), ("IsConnected", true));

        var port = Assert.Single(results);
        Assert.Equal((uint)2, port.Properties["PortIndex"].Value);
        Assert.Equal(true, port.Properties["IsConnected"].Value);
    }

    private static IReadOnlyList<PSObject> InvokeGetUSBPort(params (string Name, object Value)[] parameters)
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Commands.Add(new SessionStateCmdletEntry(
            "Get-USBPort",
            typeof(TestGetUSBPortCommand),
            null));

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var powershell = System.Management.Automation.PowerShell.Create();
        powershell.Runspace = runspace;
        powershell.AddCommand("Get-USBPort");

        foreach (var (name, value) in parameters)
        {
            powershell.AddParameter(name, value);
        }

        var results = powershell.Invoke();
        Assert.Empty(powershell.Streams.Error);
        return results;
    }
}

[Cmdlet(VerbsCommon.Get, "USBPort")]
public sealed class TestGetUSBPortCommand : GetUSBPortCommand
{
    public TestGetUSBPortCommand()
        : base(new StubUSBInspector())
    {
    }

    private sealed class StubUSBInspector : IUSBInspector
    {
        public IEnumerable<USBDeviceInfo> GetUSBDeviceInfo()
        {
            return Array.Empty<USBDeviceInfo>();
        }

        public IEnumerable<USBPortInfo> GetUSBPortInfo()
        {
            return new[]
            {
                new USBPortInfo
                {
                    HubName = "USB Root Hub",
                    HubInstanceId = "USB\\ROOT_HUB30\\A",
                    PortIndex = 1,
                    IsConnected = true,
                    ConnectionSpeed = "SuperSpeed"
                },
                new USBPortInfo
                {
                    HubName = "USB Root Hub",
                    HubInstanceId = "USB\\ROOT_HUB30\\A",
                    PortIndex = 2,
                    IsConnected = true,
                    ConnectionSpeed = "HighSpeed"
                },
                new USBPortInfo
                {
                    HubName = "External Hub",
                    HubInstanceId = "USB\\VID_1234&PID_5678\\B",
                    PortIndex = 3,
                    IsConnected = false,
                    ConnectionSpeed = "Unknown"
                }
            };
        }
    }
}
