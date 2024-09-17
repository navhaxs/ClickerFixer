using System;
using System.Text.Json;
using Makaretu.Dns;

namespace ClickerFixer.Client.Services;

public class Discovery
{
    public Discovery()
    {
        var sd = new ServiceDiscovery();
        sd.ServiceDiscovered += (EventHandler<DomainName>) ((s, serviceName) =>
        {
            Console.WriteLine(JsonSerializer.Serialize<object>(s) + " " +
                              JsonSerializer.Serialize<DomainName>(serviceName));
            // TODO initialize
            HandleClickEventService test = new HandleClickEventService();
        });
        // TODO scan specifically for clicker app here
        sd.QueryAllServices();
    }

    public static Discovery Instance { get; } = new();
}