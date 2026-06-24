// Native <dialog> control for Blazor. Takes the element reference Blazor passes
// for @ref and drives showModal()/close(), which Blazor cannot invoke directly.

export function showModal(element) {
  if (element && typeof element.showModal === "function" && !element.open) {
    element.showModal();
    // Light-dismiss: a click whose target is the dialog element itself landed on
    // the ::backdrop (clicks on real content target child elements), so close.
    // Mirrors the Vue @click.self backdrop-close behavior.
    if (!element.dataset.backdropClose) {
      element.dataset.backdropClose = "1";
      element.addEventListener("click", (e) => {
        if (e.target === element) {
          element.close();
        }
      });
    }
  }
}

export function close(element) {
  if (element && typeof element.close === "function" && element.open) {
    element.close();
  }
}
