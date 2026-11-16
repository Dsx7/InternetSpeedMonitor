using System.Net.NetworkInformation;

namespace NetPulseTaskbar.Models
{
    public class AdapterInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public NetworkInterfaceType InterfaceType { get; set; }
        public OperationalStatus Status { get; set; }
        public string IPv4Address { get; set; } = "N/A";
        public string IPv6Address { get; set; } = "N/A";
        public string GatewayAddress { get; set; } = "N/A";
        public string DnsServers { get; set; } = "N/A";
        public string MacAddress { get; set; } = "N/A";
        public long SpeedBitsPerSec { get; set; }
        public string FormattedSpeed { get; set; } = "N/A";
        public bool IsOperational => Status == OperationalStatus.Up;

        public override string ToString() => $"{Name} ({Description})";
    }
}
