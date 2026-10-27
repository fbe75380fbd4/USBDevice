namespace USBDevice.Domain.Models
{
    public class USBPortInfo
    {
        public string HubName { get; set; }
        public string HubInstanceId { get; set; }
        public uint PortIndex { get; set; }
        public bool IsConnected { get; set; }
        public string ConnectionSpeed { get; set; }
    }
}