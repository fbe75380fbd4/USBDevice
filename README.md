# USBDevice

USBDevice is a Windows-only PowerShell module for inspecting USB hubs, ports, and connected devices. It uses Windows USB APIs and requires PowerShell 5.1 or later.

## Description

The module provides cmdlets to:

- list connected USB devices, their hub and port, device identifiers, USB version, and supported and negotiated speeds
- list USB hub ports and report their connection status and speed

## Cmdlets

### Get-USBDevice

Lists connected USB devices.

```powershell
Get-USBDevice
```

Display all returned device properties:

```powershell
Get-USBDevice | Format-List *
```

### Get-USBPort

Lists USB hub ports and their connection status.

```powershell
Get-USBPort
```

List only connected ports:

```powershell
Get-USBPort | Where-Object IsConnected
```

## Platform support

USBDevice supports Windows only. Importing the module on another operating system raises a `PlatformNotSupportedException`. It has been manually tested on Windows 8.1 x64 and Windows 11 x64 with Windows PowerShell 5.1.

## License

See [LICENSE](LICENSE).
