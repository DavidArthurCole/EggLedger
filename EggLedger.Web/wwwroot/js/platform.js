export function windowSize() {
  return [globalThis.innerWidth, globalThis.innerHeight];
}

export function reload() {
  globalThis.location.reload();
}
