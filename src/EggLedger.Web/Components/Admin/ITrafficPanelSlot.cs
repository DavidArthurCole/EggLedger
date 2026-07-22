using Microsoft.AspNetCore.Components;

namespace EggLedger.Web.Components.Admin;

public interface ITrafficPanelSlot {
    RenderFragment Render();
}
