

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
