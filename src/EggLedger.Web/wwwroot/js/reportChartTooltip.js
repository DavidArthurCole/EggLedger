// Cursor-following tooltip for report charts. Fully client-side: one delegated set of
// document listeners reads data-tt / data-tt-dim off hovered elements, so no SignalR round
// trip fires per mousemove. Elements opt in with a `data-tt` attribute (newline-separated
// lines); the first line renders emphasized. Loaded once from AppHost/desktop.html.
(function () {
  if (window.__reportChartTooltipInit) return;
  window.__reportChartTooltipInit = true;

  let el = null;
  function ensure() {
    if (el) return el;
    el = document.createElement("div");
    el.className = "tooltip-floating report-tooltip";
    el.style.display = "none";
    document.body.appendChild(el);
    return el;
  }

  function targetWithTT(node) {
    return node && node.closest ? node.closest("[data-tt]") : null;
  }

  function show(t, x, y) {
    const box = ensure();
    const lines = (t.getAttribute("data-tt") || "").split("\n");
    box.innerHTML = "";
    lines.forEach(function (line, i) {
      const d = document.createElement("div");
      d.className = i === 0 ? "text-xs text-gray-200 font-medium" : "text-xs text-gray-400";
      d.textContent = line;
      box.appendChild(d);
    });
    box.style.left = x + "px";
    box.style.top = y + "px";
    box.style.display = "block";
  }

  function hide() {
    if (el) el.style.display = "none";
  }

  document.addEventListener("mouseover", function (e) {
    const t = targetWithTT(e.target);
    if (t) show(t, e.clientX, e.clientY);
  });

  document.addEventListener("mousemove", function (e) {
    if (!el || el.style.display === "none") return;
    const t = targetWithTT(e.target);
    if (t) {
      el.style.left = e.clientX + "px";
      el.style.top = e.clientY + "px";
    } else {
      hide();
    }
  });

  document.addEventListener("mouseout", function (e) {
    const t = targetWithTT(e.target);
    if (t) hide();
  });
})();
