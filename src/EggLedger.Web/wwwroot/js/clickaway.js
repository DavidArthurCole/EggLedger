// Closes a Blazor popover when a click lands outside any element of the given
// CSS class. One document-level listener per registration; the .NET ref's
// CloseFromOutside is invoked on an outside click.

export function register(dotNetRef, containerClass) {
  const handler = (e) => {
    if (!e.target.closest("." + containerClass)) {
      dotNetRef.invokeMethodAsync("CloseFromOutside");
    }
  };
  // Defer so the opening click itself does not immediately close it.
  setTimeout(() => document.addEventListener("click", handler), 0);
  return {
    dispose: () => document.removeEventListener("click", handler),
  };
}
