namespace SensEssentials;

public class InfoCommand : ObservableObject
{
    public string? Command { get; set => SetValue(ref field, value); }

    public string? ChatResponse { get; set => SetValue(ref field, value); }

    public string? DialogResponse { get; set => SetValue(ref field, value); }

    public string? URL { get; set => SetValue(ref field, value); }

    public override string? ToString() { return Command; }
}
