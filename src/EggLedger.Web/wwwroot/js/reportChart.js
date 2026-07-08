// ResizeObserver bridge for line/area charts: reports container width AND height to
// .NET so the SVG viewBox geometry matches the actual rendered box (no distortion).
export function observe(el, dotNetRef) {
  const ro = new ResizeObserver(entries => {
    for (const entry of entries) {
      const box = entry.contentBoxSize?.[0];
      const width = box?.inlineSize ?? entry.contentRect.width;
      const height = box?.blockSize ?? entry.contentRect.height;
      dotNetRef.invokeMethodAsync("OnChartResized", width, height);
    }
  });
  ro.observe(el);
  return ro;
}

export function unobserve(observer) {
  observer.disconnect();
}
