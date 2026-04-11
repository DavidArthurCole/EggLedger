// Disable Chrome default keyboard shortcuts.
// Based on https://github.com/GoogleChromeLabs/carlo/blob/master/lib/features/shortcuts.js.

const ctrlOrCmdCodes = new Set([
  'KeyD',
  'KeyE',
  'KeyG',
  'KeyN',
  'KeyO',
  'KeyP',
  // 'KeyQ',
  'KeyR',
  'KeyS',
  'KeyT',
  // 'KeyW',
  'KeyY',
  'Tab',
  'PageUp',
  'PageDown',
  // 'F4',
])
const cmdCodes = new Set(['BracketLeft', 'BracketRight', 'Comma'])
const cmdOptionCodes = new Set(['ArrowLeft', 'ArrowRight', 'KeyB'])
const ctrlShiftCodes = new Set<string>([
  // 'KeyQ',
  // 'KeyW',
])
const altCodes = new Set([
  'Home',
  'ArrowLeft',
  'ArrowRight',
  // 'F4',
])

function isDigitCode(code: string): boolean {
  return /^Digit[1-9]$/.test(code)
}

function isMac(): boolean {
  return /Mac OS X/.test(navigator.userAgent)
}

function isArrowNavigationTarget(target: EventTarget | null): boolean {
  const el = target as HTMLElement
  return !el.isContentEditable && el.nodeName !== 'INPUT' && el.nodeName !== 'TEXTAREA'
}

function shouldPreventMac(event: KeyboardEvent): boolean {
  if (!event.metaKey) return false
  if (isDigitCode(event.code)) return true
  if (ctrlOrCmdCodes.has(event.code) || cmdCodes.has(event.code)) return true
  if (event.shiftKey && cmdOptionCodes.has(event.code)) return true
  if ((event.code === 'ArrowLeft' || event.code === 'ArrowRight') && isArrowNavigationTarget(event.target)) return true
  return false
}

function shouldPreventOther(event: KeyboardEvent): boolean {
  if (event.code === 'F4') return true
  if (event.ctrlKey) {
    if (isDigitCode(event.code)) return true
    if (ctrlOrCmdCodes.has(event.code)) return true
    if (event.shiftKey && ctrlShiftCodes.has(event.code)) return true
  }
  if (event.altKey && altCodes.has(event.code)) return true
  return false
}

function preventDefaultShortcuts(event: KeyboardEvent): void {
  const prevent = isMac() ? shouldPreventMac(event) : shouldPreventOther(event)
  if (prevent) event.preventDefault()
}

export function registerShortcuts(): void {
  document.addEventListener('keydown', preventDefaultShortcuts, false)
}
