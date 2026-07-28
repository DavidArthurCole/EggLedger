using EggLedger.Web.Components;
using Microsoft.AspNetCore.Components;

namespace EggLedger.Web.Server.Components;

public sealed class ProfilePanelSlot : IProfilePanelSlot {
    public RenderFragment Render() => builder => {
        builder.OpenComponent<ProfilePanelHost>(0);
        builder.CloseComponent();
    };
}
