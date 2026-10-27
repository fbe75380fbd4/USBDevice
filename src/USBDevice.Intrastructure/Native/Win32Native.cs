using System;
using System.Runtime.InteropServices;
using System.Text;

namespace USBDevice.Infrastructure.Native
{
    internal static class Win32Native
    {
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint GENERIC_READ = 0x80000000;
        internal const uint OPEN_EXISTING = 3;
        internal const uint FILE_SHARE_WRITE = 2;
        internal const uint FILE_SHARE_READ = 1;
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        internal const uint IOCTL_USB_GET_ROOT_HUB_NAME = 0x220408;
        internal const uint IOCTL_USB_GET_NODE_INFORMATION = 0x220408;
        internal const uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX = 0x220448;
        internal const uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2 = 0x22045C;
        internal const uint IOCTL_USB_GET_NODE_CONNECTION_NAME = 0x220414;

        internal const uint IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION = 0x220410;
        internal static readonly Guid GUID_DEVINTERFACE_USB_HOST_CONTROLLER =
            new Guid("3ABF6F2D-71C4-462A-8A92-1E6861E6AF27");
        internal const byte USB_DEVICE_DESCRIPTOR_TYPE = 0x01;
        internal const byte USB_STRING_DESCRIPTOR_TYPE = 0x03;
        internal const int USB_NODE_INFORMATION_SIZE = 76;
        internal const int USB_NODE_INFORMATION_PORT_COUNT_OFFSET = 6;
        internal const int USB_NODE_CONNECTION_NAME_HEADER_SIZE = 8;
        internal const int USB_NODE_CONNECTION_NAME_BUFFER_SIZE = 2048;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_INPUT_SIZE = sizeof(uint);
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_SPEED_OFFSET = 23;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_STATUS_OFFSET = 31;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_SIZE = 16;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_LENGTH_OFFSET = 4;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_PROTOCOLS_OFFSET = 8;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS_OFFSET = 12;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_USB_3_PROTOCOL = 0x04;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_OPERATING = 0x01;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_CAPABLE = 0x02;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_PLUS_OPERATING = 0x04;
        internal const int USB_NODE_CONNECTION_INFORMATION_EX_V2_SUPER_SPEED_PLUS_CAPABLE = 0x08;
        internal const int USB_DEVICE_DESCRIPTOR_BCD_USB_OFFSET = 14;
        internal const int USB_DEVICE_DESCRIPTOR_MANUFACTURER_INDEX_OFFSET = 26;
        internal const int USB_DEVICE_DESCRIPTOR_PRODUCT_INDEX_OFFSET = 27;
        internal const int USB_DESCRIPTOR_REQUEST_HEADER_SIZE = 12;
        internal const int USB_STRING_DESCRIPTOR_BUFFER_SIZE = 267;
        internal const uint DIGCF_PRESENT = 0x00000002;
        internal const uint DIGCF_ALLCLASSES = 0x00000004;
        internal const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        internal const uint SPDRP_DEVICEDESC = 0x00000000;
        internal const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        internal const int ERROR_INSUFFICIENT_BUFFER = 122;
        internal const int ERROR_INVALID_DATA = 13;
        internal const int ERROR_NO_MORE_ITEMS = 259;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr SetupDiGetClassDevs(
            IntPtr classGuid,
            string enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            string enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetDeviceInstanceId(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            StringBuilder deviceInstanceId,
            int deviceInstanceIdSize,
            out int requiredSize);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryPropertyW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

    }
}