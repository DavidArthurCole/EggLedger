

(function () {
  if (window.__outsideClickInit) return;
  window.__outsideClickInit = true;

  const registry = new Map();

  document.addEventListener("click", function (e) {
    for (const [id, entry] of registry) {
      if (!entry.el.contains(e.target)) {
        entry.dotNetRef.invokeMethodAsync("OnOutsideClick");
      }
    }
  }, true);

  window.outsideClickRegister = function (id, el, dotNetRef) {
    registry.set(id, { el: el, dotNetRef: dotNetRef });
  };

  window.outsideClickUnregister = function (id) {
    registry.delete(id);
  };
})();
