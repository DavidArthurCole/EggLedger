using EggLedger.Web.Components.Admin;
using Microsoft.AspNetCore.Components;

namespace EggLedger.Web.Server.Bot;

public sealed class BotConfigSlot : IBotConfigSlot {
    public RenderFragment Render() => builder => {
        builder.OpenComponent<EggLedger.Web.Server.Components.Admin.BotConfigPanel>(0);
        builder.CloseComponent();
    };
}
