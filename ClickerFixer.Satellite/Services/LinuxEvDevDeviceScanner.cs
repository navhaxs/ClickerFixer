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
    /// Has EV_KEY AND no EV_REL: keeps plain keyboard-style interfaces (including a
    /// USB receiver's separate keyboard/System-Control HID interfaces, which is where
    /// a presentation clicker's next/prev buttons actually show up - as KEY_LEFT/
    /// KEY_RIGHT, confirmed in production logs), while excluding the combined
    /// mouse+buttons interface (has both EV_KEY and EV_REL) and the HDMI-CEC device
    /// (same shape). A device with EV_KEY but no EV_REL never had relative-motion
    /// noise to filter in the first place, so this can't be a regression for it.
    /// </summary>
    private static bool IsRelevant(EvDevDevice device) =>
        device.Keys is { Count: > 0 } &&
        device.RelativeAxises is not { Count: > 0 };
}
