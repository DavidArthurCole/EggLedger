namespace EggLedger.Web.Server.Components;

// Preferred spawn direction for the login provider dropdown relative to its trigger button. The JS
// positioner treats this as a preference and flips it when the menu would overflow the viewport.
public enum LoginMenuPlacement {
    BottomRight,
    BottomLeft,
    Right,
    Left,
    TopRight,
    TopLeft
}
