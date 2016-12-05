using System.Collections.Generic;
using System.Linq;

namespace Calamari.Extensibility.Docker.Commands
{
    public class NetworkCommand
    {
        public string Name { get; }

        public NetworkCommand(string name)
        {
            Name = name;
        }

        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

        string GetLabelArgs => Labels.Where(label => label.Value != null)
            .Aggregate("", (current, label) => current + $" --label {label.Key}=\"{label.Value}\"");


        public string Driver { get; set; }

        string GetNetworkType
        {
            get
            {
                if (string.IsNullOrEmpty(Driver))
                    return "";


                return $"--driver=\"{Driver}\"";
            }
        }

        public List<string> Subnets = new List<string>();
        public List<string> IpRanges = new List<string>();
        public List<string> Gateways = new List<string>();
        public string OtherArgs { get; set; }

        private string GetSubnets => string.Join(" ", Subnets.Select(subnet => $"--subnet={subnet}"));

        private string GetIpRanges =>  string.Join(" ", IpRanges.Select(range => $"--ip-range={range}"));

        private string GetGateways => string.Join(" ", Gateways.Select(gateway => $"--gateway={gateway}"));

        public override string ToString()
        {
            var args = (new[]
                {
                    GetLabelArgs,
                    GetNetworkType,
                    GetSubnets,
                    GetIpRanges,
                    GetGateways,
                    OtherArgs,
                    Name
                })
                .Where(m => !string.IsNullOrEmpty(m));

            return $"docker network create {string.Join(" ", args)}";
        }
    }
}