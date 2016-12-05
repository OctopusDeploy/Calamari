using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Calamari.Extensibility.Docker.Commands
{
    public class RunCommand
    {
        public RunCommand(string image)
        {
            Image = image;
        }

        public string Image { get; }
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();        
        public Dictionary<string, string> PortMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> AddedHosts { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> EnvironmentVariables = new Dictionary<string, string>();
        public Dictionary<string, VolumeBinding> VolumeBindings = new Dictionary<string, VolumeBinding>();
        public List<string> NetworkAliases = new List<string>();
        public List<string> VolumesFrom = new List<string>();
        public string RestartPolicy { get; set; }
        public int? RestartPolicyMax { get; set; }
        public bool PortAutoMap { get; set; }
        public string NetworkType { get; set; }
        public string NetworkContainer { get; set; }
        public string NetworkName { get; set; }
        public string VolumeDriver { get; set; }
        public string OtherArgs { get; set; }
        public string EntryCommand { get; set; }

        public bool DontRun { get; set; }
        
        public class VolumeBinding
        {
            [JsonProperty("host")]
            public string Host { get; set; }

            [JsonProperty("readOnly")]
            public string ReadOnly { get; set; }

            [JsonProperty("noCopy")]
            public string NoCopy { get; set; }

            public string CommandLineFormat(string container)
            {
                if (!string.IsNullOrEmpty(Host))
                    container = $"{Host}:{container}";

                if (!string.IsNullOrEmpty(ReadOnly) && ReadOnly.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    container = $"{container}:ro";
                }

                if (!string.IsNullOrEmpty(NoCopy) && (
                        NoCopy.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        NoCopy.Equals("nocopy", StringComparison.OrdinalIgnoreCase)))
                {
                    container = $"{container}:nocopy";
                }
                return container;
            }
        }




        string GetVolumeBindings =>  string.Join(" ", VolumeBindings.Select(b => $"--volume \"{(b.Value == null ? b.Key : b.Value.CommandLineFormat(b.Key))}\""));
        
        string GetVolumeDriver => string.IsNullOrEmpty(VolumeDriver) ? "" : $"--volume-driver=\"{VolumeDriver}\" ";

        string GetEnvironmentVariables
        {
            get
            {
                //WARNING: Encoding, line breaks etc.. this isnt great
                return string.Join(" ", EnvironmentVariables.Select(p => $"--env \"{p.Key}={p.Value}\""));
            }
        }



        private string GetVolumesFrom =>  string.Join(" ", VolumesFrom.Select(p => $"--volumes-from=\"{p}\"")); 

        
        private string GetAddedHosts => string.Join(" ", AddedHosts.Select(p => $"--add-host={p.Key}:{p.Value}")); 


        string GetNetworkType
        {
            get
            {
                if (string.IsNullOrEmpty(NetworkType))
                    return "";

                if (NetworkType.Equals("container", StringComparison.OrdinalIgnoreCase))
                {
                    return $"--network=\"container:{NetworkContainer}\"";
                }

                if (new[] {"none", "bridge", "host"}.Contains(NetworkType))
                {
                    return $"--network={NetworkType}";
                }

                return $"--network=\"{NetworkName}\"";
            }
        }


        string GetPortArgs
        {
            get
            {
                var portMappingString = string.Join(" ", PortMappings.Select(p => $"--publish {FormatPortMapping(p.Value, p.Key)}"));

                if (PortAutoMap)
                {
                    portMappingString = (string.IsNullOrEmpty(portMappingString) ? "": " ") + "--publish-all";
                }
                return portMappingString;
            }
        }

        string GetRestartPolicyArg
        {
            get
            {
                if (string.IsNullOrEmpty(RestartPolicy))
                    return string.Empty;

                var arg = $"--restart {RestartPolicy}";
                if (RestartPolicy == "on-failure" && RestartPolicyMax.HasValue)
                {
                    return $"{arg}:{RestartPolicyMax.Value}";
                }

                return arg;
            }
        }

        string GetNetworkAlias => string.Join(" ", NetworkAliases.Select(alias => $"--network-alias=\"{alias}\""));


        string GetLabelArgs => Labels.Where(label => label.Value != null)
            .Aggregate("", (current, label) => current + $" --label {label.Key}=\"{label.Value}\"");

        public override string ToString()
        {
            var command = DontRun ? "create" : "run --detach";

            var args = (new[]
                {
                    GetVolumeDriver,
                    GetVolumeBindings,
                    GetNetworkAlias,
                    GetLabelArgs,
                    GetRestartPolicyArg,
                    GetVolumesFrom,
                    GetPortArgs,
                    GetNetworkType,
                    GetEnvironmentVariables,
                    GetAddedHosts,
                    OtherArgs,
                    Image,
                    EntryCommand
                })
                .Where(m => !string.IsNullOrEmpty(m));

            return $"docker {command} {string.Join(" ", args)}".TrimEnd(' ');
        }

        static string FormatPortMapping(string host, string container)
        {
            if (string.IsNullOrEmpty(host))
            {
                return container;
            }

            int port;
            if (int.TryParse(host, out port))
            {
                return $"{host}:{container}";
            }

            return host.Contains(":") ? $"{host}:{container}" : $"{host}::{container}";
        }
    }
}