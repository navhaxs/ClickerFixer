using Makaretu.Dns;

namespace ClickerFixer.Satellite.Services;

internal class MyServiceDiscovery
{
	public MyServiceDiscovery()
	{
		ServiceProfile service = new ServiceProfile("x", "_clickerfixer._tcp", 1024);
		new ServiceDiscovery().Advertise(service);
	}
}
