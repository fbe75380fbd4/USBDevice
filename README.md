# USBDevice

USBDevice is a Windows-only PowerShell module for inspecting USB hubs, ports, and connected devices. It uses Windows USB APIs and requires PowerShell 5.1 or later.

## Description

USBDevice provides two cmdlets:

- `Get-USBDevice` lists connected devices with their hub and port, friendly name, vendor/product IDs, USB version, and maximum and negotiated speeds.
- `Get-USBPort` lists hub ports with their hub identity, port index, connection status, and connection speed. Results display as a table by default.

## Module

Install the module for the current user:

```powershell
Install-Module -Name USBDevice -Scope CurrentUser
```

Update an installed module:

```powershell
Update-Module -Name USBDevice
```

## Functions

### Get-USBDevice

Lists connected USB devices and their hub, port, identifiers, USB version, and supported and negotiated speeds. Text filters support case-insensitive PowerShell wildcards. Specify at most one text-filter parameter per command; combine it with `-PortIndex` if needed.

#### Parameters

- `-HubName` (required when used): Filter by USB hub friendly name.
- `-HubInstanceId` (required when used): Filter by USB hub instance ID.
- `-FriendlyName` (required when used): Filter by the device friendly name.
- `-UsbVersion` (required when used): Filter by USB version, such as `3.*`.
- `-PortIndex`: Filter by port index. Can be combined with any one text filter.

Each text filter accepts multiple patterns and returns objects matching any of them.

List all connected USB devices:

```powershell
Get-USBDevice
```

Filter by device name, hub name, hub instance ID, or USB version:

```powershell
Get-USBDevice -FriendlyName '*Keyboard*'
Get-USBDevice -HubName '*Root Hub*'
Get-USBDevice -HubInstanceId 'USB\ROOT_HUB30*'
Get-USBDevice -UsbVersion '3.*'
```

Combine a text filter with a port index, or inspect all properties in list form:

```powershell
Get-USBDevice -HubName '*Root Hub*' -PortIndex 3
Get-USBDevice -PortIndex 3 | Format-List *
```

Each text filter accepts multiple patterns; an object is returned when it matches any supplied pattern:

```powershell
Get-USBDevice -FriendlyName '*Keyboard*', '*Mouse*'
```

### Get-USBPort

Lists USB hub ports, with hub identity, port index, connection status, and connection speed. Text filters support case-insensitive PowerShell wildcards. Specify at most one text-filter parameter per command.

#### Parameters

- `-HubName` (required when used): Filter by USB hub friendly name.
- `-HubInstanceId` (required when used): Filter by USB hub instance ID.
- `-PortIndex`: Filter by port index; can be combined with either text filter.
- `-IsConnected`: Return only connected ports. When omitted, both connected and disconnected ports are returned.

List all ports. The cmdlet uses the module's table view by default:

```powershell
Get-USBPort
```

Filter by hub name or hub instance ID, optionally narrowing the results by port:

```powershell
Get-USBPort -HubName '*Root Hub*'
Get-USBPort -HubInstanceId 'USB\ROOT_HUB30*' -PortIndex 2
```

Show only connected ports, or use PowerShell's `Format-List` to display every property:

```powershell
Get-USBPort -IsConnected
Get-USBPort | Format-List *
```

## Platform support

USBDevice supports Windows only. Importing the module on another operating system raises a `PlatformNotSupportedException`. It has been manually tested on Windows 8.1 x64 and Windows 11 x64 with Windows PowerShell 5.1.

## License

See [LICENSE](LICENSE).
