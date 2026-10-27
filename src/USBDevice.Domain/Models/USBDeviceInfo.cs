namespace USBDevice.Domain.Models
{
    public class USBDeviceInfo
    {
        public string HubName { get; set; }
        public string HubInstanceId { get; set; }
        public uint PortIndex { get; set; }
        public string FriendlyName { get; set; }
        public string USBVersion { get; set; }
        public string VendorId { get; set; }
        public string ProductId { get; set; }
        public string MaxSupportedSpeed { get; set; }
        public string NegotiatedSpeed { get; set; }
    }
}