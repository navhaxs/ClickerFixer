using System.Net;

namespace ClickerFixer.Desktop.UI;

public class DesignMainViewModel : MainViewModel
{
    public DesignMainViewModel()
    {
        this.Discovery.ConnectedSatellites.Add(IPAddress.Loopback);
        this.Discovery.ConnectedSatellites.Add(IPAddress.Broadcast);
    }
}