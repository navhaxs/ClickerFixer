using Makaretu.Dns;

namespace ClickerFixer.Satellite.Services;

internal class MyServiceAdvertisement
{
	public MyServiceAdvertisement()
	{
        string hostName = System.Net.Dns.GetHostName();
        string instanceName = $"{hostName}#{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"; 
		ServiceProfile service = new ServiceProfile(instanceName, "_clicker._tcp", Global.ServerConfig.Port);
		new ServiceDiscovery().Advertise(service);
	}
}
