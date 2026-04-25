export async function downloadReportJson(id: string, name: string): Promise<void> {
  const json = await globalThis.exportReport(id)
  if (!json) return
  const blob = new Blob([json], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = name.replace(/[^a-z0-9-_]/gi, '_').toLowerCase() + '.json'
  a.click()
  URL.revokeObjectURL(url)
}

export function readImportFile(): Promise<string | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.json,application/json'
    input.onchange = () => {
      const file = input.files?.[0]
      if (!file) { resolve(null); return }
      const reader = new FileReader()
      reader.onload = (e) => resolve((e.target?.result as string) ?? null)
      reader.readAsText(file)
    }
    input.oncancel = () => resolve(null)
    input.click()
  })
}
