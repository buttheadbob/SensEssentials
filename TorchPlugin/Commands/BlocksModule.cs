using SensEssentials.Commands;
using Torch.Commands;

namespace SensEssentials.TorchHost.Commands;

[Category("blocks")]
public class BlocksModule : EssentialsModule
{
    private BlocksActions Actions => new(Call);

    [Command("on", "Turn on blocks matching the given conditions. Usage: !blocks on [type|subtype|general] <value> [conditions...]")]
    public void On() => Actions.On();

    [Command("off", "Turn off blocks matching the given conditions. Usage: !blocks off [type|subtype|general] <value> [conditions...]")]
    public void Off() => Actions.Off();

    [Command("remove", "Remove blocks matching the given conditions. Usage: !blocks remove [type|subtype] <value> [conditions...]")]
    public void Remove() => Actions.Remove();

    [Command("count", "Count blocks matching the given conditions. Usage: !blocks count [type|subtype|general] <value> [conditions...]")]
    public void Count() => Actions.Count();
}
