namespace ClickerFixer.Desktop;

public class CompletedAction
{
    public int Index { get; set; }
    public int KeyCode { get; set; }
    public string Target { get; set; }

    public override string ToString()
    {
        return $"Action: {Target} {KeyCode} {Index}";
    }
}