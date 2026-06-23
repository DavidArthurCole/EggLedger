// Native <dialog> control for Blazor. Takes the element reference Blazor passes
// for @ref and drives showModal()/close(), which Blazor cannot invoke directly.

export function showModal(element) {
  if (element && typeof element.showModal === "function" && !element.open) {
    element.showModal();
  }
}

export function close(element) {
  if (element && typeof element.close === "function" && element.open) {
    element.close();
  }
}
