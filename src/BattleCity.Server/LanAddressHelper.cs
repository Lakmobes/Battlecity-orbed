using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BattleCity.Server;

public static class LanAddressHelper
{
    public static IReadOnlyList<string> GetLanIPv4Addresses()
    {
        var results = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork
                    || IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                var text = address.Address.ToString();
                if (!results.Contains(text, StringComparer.Ordinal))
                {
                    results.Add(text);
                }
            }
        }

        if (results.Count == 0)
        {
            results.Add("127.0.0.1");
        }

        return results;
    }
}
