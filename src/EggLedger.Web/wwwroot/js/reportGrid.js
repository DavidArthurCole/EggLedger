// ResizeObserver bridge for the report grid: reports container width to .NET so row
// height can stay responsive (ported from the Go/Vue reference's ResizeObserver logic).
export function observe(el, dotNetRef) {
  const ro = new ResizeObserver(entries => {
    for (const entry of entries) {
      const width = entry.contentBoxSize?.[0]?.inlineSize ?? entry.contentRect.width;
      dotNetRef.invokeMethodAsync("OnGridResized", width);
    }
  });
  ro.observe(el);
  return ro;
}

export function unobserve(observer) {
  observer.disconnect();
}
