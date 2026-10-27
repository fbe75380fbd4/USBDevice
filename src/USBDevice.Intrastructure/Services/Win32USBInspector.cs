using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using USBDevice.Domain.Interfaces;
using USBDevice.Domain.Models;
using USBDevice.Infrastructure.Native;

namespace USBDevice.Infrastructure.Services
{
    public class Win32USBInspector : IUSBInspector
    {
        private sealed class HubIdentity
        {
            public string FriendlyName { get; set; }
            public string InstanceId { get; set; }
        }

        #region Public Methods
        public IEnumerable<USBDeviceInfo> GetUSBDeviceInfo()
        {
            var results = new List<USBDeviceInfo>();
            Dictionary<string, string> friendlyNames = GetPnpFriendlyNames();
            Dictionary<string, HubIdentity> hubIdentities = GetHubIdentities();
            foreach (string hcdPath in GetHostControllerPaths())
            {
                IntPtr hCData = Win32Native.CreateFile(
                    hcdPath,
                    Win32Native.GENERIC_READ | Win32Native.GENERIC_WRITE,
                    Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Native.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
                if (hCData == Win32Native.INVALID_HANDLE_VALUE) continue;

                try
                {
                    string rootHubName = GetRootHubName(hCData);
                    if (!string.IsNullOrEmpty(rootHubName))
                    {
                        EnumerateHubForDeviceDetails(
                            rootHubName,
                            results,
                            friendlyNames,
                            hubIdentities,
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    }
                }
                finally { Win32Native.CloseHandle(hCData); }
            }
            return results;
        }

        public IEnumerable<USBPortInfo> GetUSBPortInfo()
        {
            var results = new List<USBPortInfo>();
            Dictionary<string, HubIdentity> hubIdentities = GetHubIdentities();
            foreach (string hcdPath in GetHostControllerPaths())
            {
                IntPtr hCData = Win32Native.CreateFile(
                    hcdPath,
                    Win32Native.GENERIC_READ | Win32Native.GENERIC_WRITE,
                    Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Native.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
                if (hCData == Win32Native.INVALID_HANDLE_VALUE) continue;

                try
                {
                    string rootHubName = GetRootHubName(hCData);
                    if (!string.IsNullOrEmpty(rootHubName))
                    {
                        EnumerateHubPorts(
                            rootHubName,
                            results,
                            hubIdentities,
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    }
                }
                finally { Win32Native.CloseHandle(hCData); }
            }
            return results;
        }

        #endregion

        #region Private Methods
        private string BuildFriendlyName(
            string pnpFriendlyName,
            string manufacturer,
            string product)
        {
            if (string.Equals(manufacturer, "USB Device", StringComparison.OrdinalIgnoreCase))
            {
                manufacturer = null;
            }

            string name;
            if (string.IsNullOrWhiteSpace(product))
            {
                name = pnpFriendlyName;
            }
            else if (string.IsNullOrWhiteSpace(manufacturer) ||
                product.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase))
            {
                name = product;
            }
            else
            {
                name = $"{manufacturer} {product}";
            }

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private void CheckPortConnection(
            IntPtr hHub,
            uint portIndex,
            HubIdentity hubIdentity,
            List<USBPortInfo> results)
        {
            int bufferSize = 1024;
            IntPtr inputPtr = Marshal.AllocHGlobal(Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_INPUT_SIZE);
            IntPtr bufferPtr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                Marshal.WriteInt32(inputPtr, 0, (int)portIndex);

                uint bytesReturned = 0;
                bool success = Win32Native.DeviceIoControl(
                    hHub,
                    Win32Native.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX,
                    inputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_INPUT_SIZE,
                    bufferPtr,
                    (uint)bufferSize,
                    out bytesReturned,
                    IntPtr.Zero);
                if (!success) return;

                int connectionStatus = Marshal.ReadInt32(
                    bufferPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_STATUS_OFFSET);
                bool isConnected = (connectionStatus == 1);

                string speedText = "Disconnected";
                if (isConnected)
                {
                    int speedEnum = Marshal.ReadByte(
                        bufferPtr,
                        Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_SPEED_OFFSET);
                    int? connectionSpeedFlags = GetConnectionSpeedFlags(hHub, portIndex);
                    speedText = GetNegotiatedSpeed(speedEnum, connectionSpeedFlags);
                }

                results.Add(new USBPortInfo
                {
                    HubName = hubIdentity.FriendlyName,
                    HubInstanceId = hubIdentity.InstanceId,
                    PortIndex = portIndex,
                    IsConnected = isConnected,
                    ConnectionSpeed = speedText
                });
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
                Marshal.FreeHGlobal(bufferPtr);
            }
        }

        private void EnumerateHubForDeviceDetails(
            string hubSymbolicName,
            List<USBDeviceInfo> results,
            Dictionary<string, string> friendlyNames,
            Dictionary<string, HubIdentity> hubIdentities,
            HashSet<string> visitedHubs)
        {
            if (!visitedHubs.Add(hubSymbolicName)) return;

            IntPtr hHub = Win32Native.CreateFile(
                hubSymbolicName,
                Win32Native.GENERIC_READ | Win32Native.GENERIC_WRITE,
                Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Native.OPEN_EXISTING,
                0,
                IntPtr.Zero);
            if (hHub == Win32Native.INVALID_HANDLE_VALUE) return;

            HubIdentity hubIdentity = ResolveHubIdentity(hubSymbolicName, hubIdentities);
            try
            {
                uint bytesReturned = 0;
                int nodeInfoSize = Win32Native.USB_NODE_INFORMATION_SIZE;
                IntPtr nodeInfoPtr = Marshal.AllocHGlobal(nodeInfoSize);

                try
                {
                    for (int k = 0; k < nodeInfoSize; k++) Marshal.WriteByte(nodeInfoPtr, k, 0);

                    bool success = Win32Native.DeviceIoControl(
                        hHub,
                        Win32Native.IOCTL_USB_GET_NODE_INFORMATION,
                        nodeInfoPtr,
                        (uint)nodeInfoSize,
                        nodeInfoPtr,
                        (uint)nodeInfoSize,
                        out bytesReturned,
                        IntPtr.Zero);
                    if (!success) return;

                    uint totalPorts = Marshal.ReadByte(
                        nodeInfoPtr,
                        Win32Native.USB_NODE_INFORMATION_PORT_COUNT_OFFSET);

                    for (uint portIndex = 1; portIndex <= totalPorts; portIndex++)
                    {
                        ExtractDeviceDetailsFromPort(
                            hHub,
                            portIndex,
                            hubIdentity,
                            results,
                            friendlyNames);

                        string downstreamHubName = GetConnectedHubName(hHub, portIndex);
                        if (!string.IsNullOrEmpty(downstreamHubName))
                        {
                            EnumerateHubForDeviceDetails(
                                downstreamHubName,
                                results,
                                friendlyNames,
                                hubIdentities,
                                visitedHubs);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(nodeInfoPtr); }
            }
            finally { Win32Native.CloseHandle(hHub); }
        }

        private void EnumerateHubPorts(
            string hubSymbolicName,
            List<USBPortInfo> results,
            Dictionary<string, HubIdentity> hubIdentities,
            HashSet<string> visitedHubs)
        {
            if (!visitedHubs.Add(hubSymbolicName)) return;

            IntPtr hHub = Win32Native.CreateFile(
                hubSymbolicName,
                Win32Native.GENERIC_READ | Win32Native.GENERIC_WRITE,
                Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Native.OPEN_EXISTING,
                0,
                IntPtr.Zero);
            if (hHub == Win32Native.INVALID_HANDLE_VALUE) return;

            HubIdentity hubIdentity = ResolveHubIdentity(hubSymbolicName, hubIdentities);
            try
            {
                uint bytesReturned = 0;
                int nodeInfoSize = Win32Native.USB_NODE_INFORMATION_SIZE;
                IntPtr nodeInfoPtr = Marshal.AllocHGlobal(nodeInfoSize);

                try
                {
                    for (int i = 0; i < nodeInfoSize; i++) Marshal.WriteByte(nodeInfoPtr, i, 0);

                    bool success = Win32Native.DeviceIoControl(
                        hHub,
                        Win32Native.IOCTL_USB_GET_NODE_INFORMATION,
                        nodeInfoPtr,
                        (uint)nodeInfoSize,
                        nodeInfoPtr,
                        (uint)nodeInfoSize,
                        out bytesReturned,
                        IntPtr.Zero);
                    if (!success) return;

                    uint totalPorts = Marshal.ReadByte(
                        nodeInfoPtr,
                        Win32Native.USB_NODE_INFORMATION_PORT_COUNT_OFFSET);

                    for (uint portIndex = 1; portIndex <= totalPorts; portIndex++)
                    {
                        CheckPortConnection(hHub, portIndex, hubIdentity, results);
                        string downstreamHubName = GetConnectedHubName(hHub, portIndex);
                        if (!string.IsNullOrEmpty(downstreamHubName))
                        {
                            EnumerateHubPorts(
                                downstreamHubName,
                                results,
                                hubIdentities,
                                visitedHubs);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(nodeInfoPtr); }
            }
            finally { Win32Native.CloseHandle(hHub); }
        }

        private void ExtractDeviceDetailsFromPort(
            IntPtr hHub,
            uint portIndex,
            HubIdentity hubIdentity,
            List<USBDeviceInfo> results,
            Dictionary<string, string> friendlyNames)
        {
            int bufferSize = 1024;
            IntPtr inputPtr = Marshal.AllocHGlobal(Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_INPUT_SIZE);
            IntPtr bufferPtr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                Marshal.WriteInt32(inputPtr, 0, (int)portIndex);

                uint bytesReturned = 0;
                bool success = Win32Native.DeviceIoControl(
                    hHub,
                    Win32Native.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX,
                    inputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_INPUT_SIZE,
                    bufferPtr,
                    (uint)bufferSize,
                    out bytesReturned,
                    IntPtr.Zero);
                if (!success) return;

                int connectionStatus = Marshal.ReadInt32(
                    bufferPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_STATUS_OFFSET);
                if (connectionStatus != 1) return; // Not connected

                int negotiatedSpeedEnum = Marshal.ReadByte(
                    bufferPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_SPEED_OFFSET);
                int? connectionSpeedFlags = GetConnectionSpeedFlags(hHub, portIndex);
                string negotiatedSpeed = GetNegotiatedSpeed(
                    negotiatedSpeedEnum,
                    connectionSpeedFlags);

                int vid = 0;
                int pid = 0;
                int bcdUsb = 0;
                int manufacturerIndex = 0;
                int productIndex = 0;

                IntPtr descReqPtr = Marshal.AllocHGlobal(512);
                try
                {
                    for (int i = 0; i < 512; i++) Marshal.WriteByte(descReqPtr, i, 0);

                    Marshal.WriteInt32(descReqPtr, 0, (int)portIndex);
                    Marshal.WriteByte(descReqPtr, 4, 0x80);
                    Marshal.WriteByte(descReqPtr, 5, 0x06);
                    Marshal.WriteInt16(descReqPtr, 6, (short)(Win32Native.USB_DEVICE_DESCRIPTOR_TYPE << 8));
                    Marshal.WriteInt16(descReqPtr, 8, 0);
                    Marshal.WriteInt16(descReqPtr, 10, 18);

                    bool descSuccess = Win32Native.DeviceIoControl(
                        hHub,
                        Win32Native.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                        descReqPtr,
                        Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE,
                        descReqPtr,
                        512,
                        out uint descBytesRet,
                        IntPtr.Zero);

                    if (descSuccess && descBytesRet >= 18)
                    {
                        bcdUsb = Marshal.ReadInt16(
                            descReqPtr,
                            Win32Native.USB_DEVICE_DESCRIPTOR_BCD_USB_OFFSET) & 0xFFFF;
                        manufacturerIndex = Marshal.ReadByte(
                            descReqPtr,
                            Win32Native.USB_DEVICE_DESCRIPTOR_MANUFACTURER_INDEX_OFFSET);
                        productIndex = Marshal.ReadByte(
                            descReqPtr,
                            Win32Native.USB_DEVICE_DESCRIPTOR_PRODUCT_INDEX_OFFSET);
                        vid = Marshal.ReadInt16(descReqPtr, 20) & 0xFFFF;
                        pid = Marshal.ReadInt16(descReqPtr, 22) & 0xFFFF;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(descReqPtr);
                }

                string pnpFriendlyName = friendlyNames.TryGetValue(
                    GetVidPidKey(vid, pid),
                    out string matchedFriendlyName)
                    ? matchedFriendlyName
                    : null;
                string manufacturer = GetUsbStringDescriptor(hHub, portIndex, manufacturerIndex);
                string product = GetUsbStringDescriptor(hHub, portIndex, productIndex);

                results.Add(new USBDeviceInfo
                {
                    HubName = hubIdentity.FriendlyName,
                    HubInstanceId = hubIdentity.InstanceId,
                    PortIndex = portIndex,
                    FriendlyName = BuildFriendlyName(pnpFriendlyName, manufacturer, product),
                    USBVersion = FormatUsbVersion(bcdUsb),
                    VendorId = $"0x{vid:X4}",
                    ProductId = $"0x{pid:X4}",
                    MaxSupportedSpeed = GetMaxSupportedSpeed(
                        connectionSpeedFlags,
                        negotiatedSpeedEnum,
                        bcdUsb),
                    NegotiatedSpeed = negotiatedSpeed
                });
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
                Marshal.FreeHGlobal(bufferPtr);
            }
        }

        private string FormatUsbVersion(int bcdUsb)
        {
            if (bcdUsb == 0)
            {
                return "Unknown";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:X1}.{1:X1}{2:X1}",
                (bcdUsb >> 8) & 0xFF,
                (bcdUsb >> 4) & 0xF,
                bcdUsb & 0xF);
        }

        private string GetConnectedHubName(IntPtr hHub, uint portIndex)
        {
            int bufferSize = Win32Native.USB_NODE_CONNECTION_NAME_BUFFER_SIZE;
            IntPtr bufferPtr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                for (int i = 0; i < bufferSize; i++)
                {
                    Marshal.WriteByte(bufferPtr, i, 0);
                }

                Marshal.WriteInt32(bufferPtr, 0, (int)portIndex);

                bool success = Win32Native.DeviceIoControl(
                    hHub,
                    Win32Native.IOCTL_USB_GET_NODE_CONNECTION_NAME,
                    bufferPtr,
                    (uint)bufferSize,
                    bufferPtr,
                    (uint)bufferSize,
                    out _,
                    IntPtr.Zero);
                if (!success)
                {
                    return null;
                }

                int actualLength = Marshal.ReadInt32(bufferPtr, sizeof(uint));
                if (actualLength <= Win32Native.USB_NODE_CONNECTION_NAME_HEADER_SIZE ||
                    actualLength > bufferSize)
                {
                    return null;
                }

                string hubName = Marshal.PtrToStringUni(
                    IntPtr.Add(bufferPtr, Win32Native.USB_NODE_CONNECTION_NAME_HEADER_SIZE));
                return string.IsNullOrWhiteSpace(hubName) ? null : $@"\\.\{hubName}";
            }
            finally
            {
                Marshal.FreeHGlobal(bufferPtr);
            }
        }

        private int? GetConnectionSpeedFlags(IntPtr hHub, uint portIndex)
        {
            IntPtr inputPtr = Marshal.AllocHGlobal(
                Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE);
            IntPtr outputPtr = Marshal.AllocHGlobal(
                Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE);

            try
            {
                for (int i = 0; i < Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE; i++)
                {
                    Marshal.WriteByte(inputPtr, i, 0);
                    Marshal.WriteByte(outputPtr, i, 0);
                }

                Marshal.WriteInt32(inputPtr, 0, (int)portIndex);
                Marshal.WriteInt32(
                    inputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_LENGTH_OFFSET,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE);
                Marshal.WriteInt32(
                    inputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_PROTOCOLS_OFFSET,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_USB_3_PROTOCOL);

                bool success = Win32Native.DeviceIoControl(
                    hHub,
                    Win32Native.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2,
                    inputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE,
                    outputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE,
                    out uint bytesReturned,
                    IntPtr.Zero);

                if (!success || bytesReturned < Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE)
                {
                    return null;
                }

                return Marshal.ReadInt32(
                    outputPtr,
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS_OFFSET);
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
                Marshal.FreeHGlobal(outputPtr);
            }
        }

        private string GetDeviceFriendlyName(
            IntPtr deviceInfoSet,
            ref Win32Native.SP_DEVINFO_DATA deviceInfo)
        {
            string friendlyName = GetDeviceProperty(
                deviceInfoSet,
                ref deviceInfo,
                Win32Native.SPDRP_FRIENDLYNAME);

            return string.IsNullOrWhiteSpace(friendlyName)
                ? GetDeviceProperty(deviceInfoSet, ref deviceInfo, Win32Native.SPDRP_DEVICEDESC)
                : friendlyName;
        }

        private string GetDeviceInstanceId(
            IntPtr deviceInfoSet,
            ref Win32Native.SP_DEVINFO_DATA deviceInfo)
        {
            Win32Native.SetupDiGetDeviceInstanceId(
                deviceInfoSet,
                ref deviceInfo,
                null,
                0,
                out int requiredSize);

            int error = Marshal.GetLastWin32Error();
            if (requiredSize == 0 && error != Win32Native.ERROR_INSUFFICIENT_BUFFER)
            {
                throw new Win32Exception(error);
            }

            var instanceId = new StringBuilder(requiredSize);
            if (!Win32Native.SetupDiGetDeviceInstanceId(
                deviceInfoSet,
                ref deviceInfo,
                instanceId,
                instanceId.Capacity,
                out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return instanceId.ToString();
        }

        private string GetDeviceProperty(
            IntPtr deviceInfoSet,
            ref Win32Native.SP_DEVINFO_DATA deviceInfo,
            uint property)
        {
            Win32Native.SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref deviceInfo,
                property,
                out _,
                null,
                0,
                out uint requiredSize);

            int error = Marshal.GetLastWin32Error();
            if (requiredSize == 0)
            {
                if (error == Win32Native.ERROR_INVALID_DATA)
                {
                    return null;
                }

                if (error != 0)
                {
                    throw new Win32Exception(error);
                }

                return null;
            }

            var propertyBuffer = new byte[requiredSize];
            if (!Win32Native.SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref deviceInfo,
                property,
                out _,
                propertyBuffer,
                (uint)propertyBuffer.Length,
                out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return Encoding.Unicode.GetString(propertyBuffer).TrimEnd('\0');
        }

        private List<string> GetHostControllerPaths()
        {
            var hostControllerPaths = new List<string>();
            Guid interfaceGuid = Win32Native.GUID_DEVINTERFACE_USB_HOST_CONTROLLER;
            IntPtr deviceInfoSet = Win32Native.SetupDiGetClassDevs(
                ref interfaceGuid,
                null,
                IntPtr.Zero,
                Win32Native.DIGCF_PRESENT | Win32Native.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == Win32Native.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    var deviceInterface = new Win32Native.SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(Win32Native.SP_DEVICE_INTERFACE_DATA))
                    };
                    if (!Win32Native.SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref interfaceGuid,
                        index,
                        ref deviceInterface))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == Win32Native.ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        throw new Win32Exception(error);
                    }

                    var deviceInfo = new Win32Native.SP_DEVINFO_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(Win32Native.SP_DEVINFO_DATA))
                    };
                    Win32Native.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref deviceInterface,
                        IntPtr.Zero,
                        0,
                        out uint requiredSize,
                        ref deviceInfo);
                    int detailError = Marshal.GetLastWin32Error();
                    if (requiredSize == 0)
                    {
                        throw new Win32Exception(detailError);
                    }

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                        if (!Win32Native.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            ref deviceInterface,
                            detailBuffer,
                            requiredSize,
                            out _,
                            ref deviceInfo))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        string interfacePath = Marshal.PtrToStringUni(
                            IntPtr.Add(detailBuffer, sizeof(uint)));
                        if (!string.IsNullOrWhiteSpace(interfacePath))
                        {
                            hostControllerPaths.Add(interfacePath);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }
            }
            finally
            {
                Win32Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return hostControllerPaths;
        }

        private Dictionary<string, HubIdentity> GetHubIdentities()
        {
            var hubIdentities = new Dictionary<string, HubIdentity>(StringComparer.OrdinalIgnoreCase);
            Guid usbHubInterfaceGuid = new Guid("F18A0E88-C30C-11D0-8815-00A0C906BED8");
            IntPtr deviceInfoSet = Win32Native.SetupDiGetClassDevs(
                ref usbHubInterfaceGuid,
                null,
                IntPtr.Zero,
                Win32Native.DIGCF_PRESENT | Win32Native.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == Win32Native.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    var deviceInterface = new Win32Native.SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(Win32Native.SP_DEVICE_INTERFACE_DATA))
                    };
                    if (!Win32Native.SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref usbHubInterfaceGuid,
                        index,
                        ref deviceInterface))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == Win32Native.ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        throw new Win32Exception(error);
                    }

                    var deviceInfo = new Win32Native.SP_DEVINFO_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(Win32Native.SP_DEVINFO_DATA))
                    };
                    Win32Native.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref deviceInterface,
                        IntPtr.Zero,
                        0,
                        out uint requiredSize,
                        ref deviceInfo);
                    int detailError = Marshal.GetLastWin32Error();
                    if (requiredSize == 0)
                    {
                        throw new Win32Exception(detailError);
                    }

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(
                            detailBuffer,
                            IntPtr.Size == 8 ? 8 : 6);
                        if (!Win32Native.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            ref deviceInterface,
                            detailBuffer,
                            requiredSize,
                            out _,
                            ref deviceInfo))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        string interfacePath = Marshal.PtrToStringUni(
                            IntPtr.Add(detailBuffer, sizeof(uint)));
                        if (string.IsNullOrWhiteSpace(interfacePath))
                        {
                            continue;
                        }

                        hubIdentities[NormalizeHubInterfacePath(interfacePath)] = new HubIdentity
                        {
                            FriendlyName = GetDeviceFriendlyName(deviceInfoSet, ref deviceInfo) ?? "USB Hub",
                            InstanceId = GetDeviceInstanceId(deviceInfoSet, ref deviceInfo)
                        };
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }
            }
            finally
            {
                Win32Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return hubIdentities;
        }

        private string GetMaxSupportedSpeed(
            int? connectionSpeedFlags,
            int negotiatedSpeed,
            int bcdUsb)
        {
            if (connectionSpeedFlags.HasValue)
            {
                if ((connectionSpeedFlags.Value &
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_PLUS_CAPABLE) != 0)
                {
                    return TranslateSpeed(4);
                }

                if ((connectionSpeedFlags.Value &
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_CAPABLE) != 0)
                {
                    return TranslateSpeed(3);
                }
            }

            if (negotiatedSpeed >= 2)
            {
                return TranslateSpeed(negotiatedSpeed);
            }

            if (bcdUsb > 0 && bcdUsb < 0x0200)
            {
                return TranslateSpeed(negotiatedSpeed);
            }

            return "Unknown";
        }

        private string GetNegotiatedSpeed(int legacySpeed, int? connectionSpeedFlags)
        {
            if (connectionSpeedFlags.HasValue)
            {
                if ((connectionSpeedFlags.Value &
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_PLUS_OPERATING) != 0)
                {
                    return TranslateSpeed(4);
                }

                if ((connectionSpeedFlags.Value &
                    Win32Native.USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_OPERATING) != 0)
                {
                    return TranslateSpeed(3);
                }
            }

            return TranslateSpeed(legacySpeed);
        }

        private Dictionary<string, string> GetPnpFriendlyNames()
        {
            var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IntPtr deviceInfoSet = Win32Native.SetupDiGetClassDevs(
                IntPtr.Zero,
                "USB",
                IntPtr.Zero,
                Win32Native.DIGCF_PRESENT | Win32Native.DIGCF_ALLCLASSES);

            if (deviceInfoSet == Win32Native.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    var deviceInfo = new Win32Native.SP_DEVINFO_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(Win32Native.SP_DEVINFO_DATA))
                    };

                    if (!Win32Native.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == Win32Native.ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        throw new Win32Exception(error);
                    }

                    string instanceId = GetDeviceInstanceId(deviceInfoSet, ref deviceInfo);
                    if (!TryGetVidPidKey(instanceId, out string key))
                    {
                        continue;
                    }

                    string friendlyName = GetDeviceFriendlyName(deviceInfoSet, ref deviceInfo);
                    if (string.IsNullOrWhiteSpace(friendlyName))
                    {
                        continue;
                    }

                    int priority = instanceId.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) >= 0
                        ? 0
                        : 1;
                    if (!priorities.TryGetValue(key, out int currentPriority) || priority < currentPriority)
                    {
                        friendlyNames[key] = friendlyName;
                        priorities[key] = priority;
                    }
                }
            }
            finally
            {
                Win32Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return friendlyNames;
        }

        private string GetRootHubName(IntPtr hCData)
        {
            uint bytesReturned = 0;
            byte[] stackSizeBuf = new byte[512];
            IntPtr bufferPtr = Marshal.AllocHGlobal(stackSizeBuf.Length);

            try
            {
                bool success = Win32Native.DeviceIoControl(
                    hCData,
                    Win32Native.IOCTL_USB_GET_ROOT_HUB_NAME,
                    IntPtr.Zero,
                    0,
                    bufferPtr,
                    (uint)stackSizeBuf.Length,
                    out bytesReturned,
                    IntPtr.Zero);
                if (!success) return null;

                int actualLength = Marshal.ReadInt32(bufferPtr);
                if (actualLength > stackSizeBuf.Length)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                    bufferPtr = Marshal.AllocHGlobal(actualLength);
                    success = Win32Native.DeviceIoControl(
                        hCData,
                        Win32Native.IOCTL_USB_GET_ROOT_HUB_NAME,
                        IntPtr.Zero,
                        0,
                        bufferPtr,
                        (uint)actualLength,
                        out bytesReturned,
                        IntPtr.Zero);
                    if (!success) return null;
                }

                string hubName = Marshal.PtrToStringUni(new IntPtr(bufferPtr.ToInt64() + 4));
                return $@"\\.\{hubName}";
            }
            finally { Marshal.FreeHGlobal(bufferPtr); }
        }

        private ushort GetUsbLanguageId(IntPtr hHub, uint portIndex, IntPtr descriptorRequest)
        {
            InitializeStringDescriptorRequest(descriptorRequest, portIndex, 0, 0);

            bool success = Win32Native.DeviceIoControl(
                hHub,
                Win32Native.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                descriptorRequest,
                Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE,
                descriptorRequest,
                Win32Native.USB_STRING_DESCRIPTOR_BUFFER_SIZE,
                out uint bytesReturned,
                IntPtr.Zero);
            if (!success || bytesReturned < Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE + 4)
            {
                return 0;
            }

            int descriptorLength = Marshal.ReadByte(
                descriptorRequest,
                Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE);
            return descriptorLength >= 4 ? (ushort)Marshal.ReadInt16(
                descriptorRequest,
                Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE + 2) : (ushort)0;
        }

        private string GetUsbStringDescriptor(IntPtr hHub, uint portIndex, int descriptorIndex)
        {
            if (descriptorIndex <= 0 || descriptorIndex > byte.MaxValue)
            {
                return null;
            }

            IntPtr descriptorRequest = Marshal.AllocHGlobal(
                Win32Native.USB_STRING_DESCRIPTOR_BUFFER_SIZE);

            try
            {
                ushort languageId = GetUsbLanguageId(hHub, portIndex, descriptorRequest);
                if (languageId == 0)
                {
                    return null;
                }

                InitializeStringDescriptorRequest(
                    descriptorRequest,
                    portIndex,
                    descriptorIndex,
                    languageId);

                bool success = Win32Native.DeviceIoControl(
                    hHub,
                    Win32Native.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                    descriptorRequest,
                    Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE,
                    descriptorRequest,
                    Win32Native.USB_STRING_DESCRIPTOR_BUFFER_SIZE,
                    out uint bytesReturned,
                    IntPtr.Zero);
                if (!success || bytesReturned <= Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE)
                {
                    return null;
                }

                int descriptorLength = Marshal.ReadByte(
                    descriptorRequest,
                    Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE);
                int stringLength = descriptorLength - 2;
                if (stringLength <= 0 ||
                    descriptorLength > bytesReturned - Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE)
                {
                    return null;
                }

                return Marshal.PtrToStringUni(
                    IntPtr.Add(
                        descriptorRequest,
                        Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE + 2),
                    stringLength / 2)?.Trim().TrimEnd('\0');
            }
            finally
            {
                Marshal.FreeHGlobal(descriptorRequest);
            }
        }

        private string GetVidPidKey(int vendorId, int productId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:X4}:{1:X4}", vendorId, productId);
        }

        private void InitializeStringDescriptorRequest(
            IntPtr descriptorRequest,
            uint portIndex,
            int descriptorIndex,
            ushort languageId)
        {
            for (int i = 0; i < Win32Native.USB_STRING_DESCRIPTOR_BUFFER_SIZE; i++)
            {
                Marshal.WriteByte(descriptorRequest, i, 0);
            }

            Marshal.WriteInt32(descriptorRequest, 0, (int)portIndex);
            Marshal.WriteByte(descriptorRequest, 4, 0x80);
            Marshal.WriteByte(descriptorRequest, 5, 0x06);
            Marshal.WriteInt16(
                descriptorRequest,
                6,
                (short)((Win32Native.USB_STRING_DESCRIPTOR_TYPE << 8) | descriptorIndex));
            Marshal.WriteInt16(descriptorRequest, 8, (short)languageId);
            Marshal.WriteInt16(
                descriptorRequest,
                10,
                Win32Native.USB_STRING_DESCRIPTOR_BUFFER_SIZE -
                    Win32Native.USB_DESCRIPTOR_REQUEST_HEADER_SIZE);
        }

        private string NormalizeHubInterfacePath(string interfacePath)
        {
            if (interfacePath.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                interfacePath.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
            {
                interfacePath = interfacePath.Substring(4);
            }

            return interfacePath.TrimEnd('\\');
        }

        private HubIdentity ResolveHubIdentity(
            string hubSymbolicName,
            Dictionary<string, HubIdentity> hubIdentities)
        {
            string interfacePath = NormalizeHubInterfacePath(hubSymbolicName);
            return hubIdentities.TryGetValue(interfacePath, out HubIdentity hubIdentity)
                ? hubIdentity
                : new HubIdentity { FriendlyName = "USB Hub" };
        }

        private string TranslateSpeed(int speedEnum)
        {
            return speedEnum switch
            {
                0 => "LowSpeed (1.5 Mbps)",
                1 => "FullSpeed (12 Mbps)",
                2 => "HighSpeed (480 Mbps)",
                3 => "SuperSpeed (5 Gbps)",
                4 => "SuperSpeedPlus (10+ Gbps)",
                _ => $"Connected (Speed Code: {speedEnum})"
            };
        }

        private bool TryGetVidPidKey(string instanceId, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(instanceId) ||
                !instanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int vendorIndex = instanceId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
            int productIndex = vendorIndex < 0
                ? -1
                : instanceId.IndexOf("&PID_", vendorIndex + 4, StringComparison.OrdinalIgnoreCase);

            if (vendorIndex < 0 || productIndex != vendorIndex + 8 || instanceId.Length < productIndex + 9 ||
                !int.TryParse(instanceId.Substring(vendorIndex + 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int vendorId) ||
                !int.TryParse(instanceId.Substring(productIndex + 5, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int productId))
            {
                return false;
            }

            key = GetVidPidKey(vendorId, productId);
            return true;
        }

        #endregion

    }
}