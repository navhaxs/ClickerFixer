using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal sealed class LinuxEvDevDeviceScanner : IEvDevDeviceScanner
{
    public IReadOnlyList<IEvDevDeviceHandle> Scan()
    {
        var all = EvDevDevice.GetDevices().OrderBy(d => d.DevicePath).ToList();
        var skipped = all.Where(d => !IsRelevant(d)).ToList();

        if (skipped.Count > 0)
        {
            Console.WriteLine(
                "[evdev] skipping " + skipped.Count + " irrelevant device(s): " +
                string.Join(", ", skipped.Select(d => $"{d.DevicePath} ({d.Name})")));
        }

        return all
            .Where(IsRelevant)
            .Select(d => (IEvDevDeviceHandle)new EvDevDeviceHandle(d))
            .ToList();
    }

    /// <summary>
    /// Only devices that look like a plain button/keyboard interface are worth a
    /// dedicated monitoring thread - the app only ever handles OnKeyEvent, nothing
    /// subscribes to OnRelativeEvent/OnSwitchEvent/etc. Registering every enumerated
    /// evdev node unconditionally (mouse motion, the HDMI hotplug switch, HDMI-CEC
    /// remote codes, ...) used to mean a dedicated blocking-read thread for each one;
    /// a mouse sensor sitting idle commonly emits continuous low-level EV_REL jitter,
    /// which was measurable, sustained CPU spent dispatching events nobody uses.
    ///
    /// Has EV_KEY, and not a genuine 2D pointer device: keeps plain keyboard-style
    /// interfaces. An earlier version of this filter excluded ANY device with ANY
    /// relative axis at all, which turned out to be wrong in production - a Logitech
    /// receiver's "Keyboard" HID interface (where a presentation clicker's next/prev
    /// buttons actually show up, as KEY_LEFT/KEY_RIGHT, confirmed in production logs)
    /// commonly multiplexes an unrelated REL_WHEEL axis for a volume/scroll control
    /// onto the same interface, and got wrongly excluded as if it were a mouse. Real
    /// 2D pointer motion needs REL_X *and* REL_Y together (matching the vendored
    /// library's own GuessDeviceType() mouse heuristic) - a lone REL_WHEEL doesn't
    /// have both, so this now correctly keeps that interface while still excluding
    /// real mice and the HDMI-CEC device (both report REL_X+REL_Y).
    /// </summary>
    private static bool IsRelevant(EvDevDevice device)
    {
        if (device.Keys is not { Count: > 0 })
            return false;

        var relativeAxes = device.RelativeAxises;
        bool isPointerDevice = relativeAxes != null
            && relativeAxes.Contains(EvDevRelativeAxisCode.REL_X)
            && relativeAxes.Contains(EvDevRelativeAxisCode.REL_Y);

        return !isPointerDevice;
    }
}
