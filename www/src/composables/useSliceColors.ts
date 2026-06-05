import { ref, type Ref } from 'vue'

function hexToHsl(hex: string): [number, number, number] {
  const rv = Number.parseInt(hex.slice(1, 3), 16) / 255
  const gv = Number.parseInt(hex.slice(3, 5), 16) / 255
  const bv = Number.parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(rv, gv, bv)
  const min = Math.min(rv, gv, bv)
  const l = (max + min) / 2
  const d = max - min
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1))
  let h = 0
  if (d !== 0) {
    switch (max) {
      case rv: h = ((gv - bv) / d + 6) % 6; break
      case gv: h = (bv - rv) / d + 2; break
      default: h = (rv - gv) / d + 4
    }
    h *= 60
  }
  return [h, s, l]
}

function hslToHex(h: number, s: number, l: number): string {
  const a = s * Math.min(l, 1 - l)
  const f = (n: number) => {
    const k = (n + h / 30) % 12
    const color = l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1)
    return Math.round(255 * color).toString(16).padStart(2, '0')
  }
  return `#${f(0)}${f(8)}${f(4)}`
}

function autoSliceColors(baseColor: string, count: number): string[] {
  const [h, s, l] = hexToHsl(baseColor)
  return Array.from({ length: count }, (_, i) =>
    hslToHex(((h + (i * 360) / count) % 360), s, l),
  )
}

/**
 * Owns per-label chart colors for pie and bar charts. Labels without an explicit
 * override fall back to an auto-generated hue spread derived from baseColor.
 */
export function useSliceColors(baseColor: Ref<string>, chartLabels: Ref<string[]>) {
  const labelColorsMap = ref<Record<string, string>>({})

  function parseLabelColors(raw: string): Record<string, string> {
    if (!raw) return {}
    try {
      return JSON.parse(raw) as Record<string, string>
    } catch {
      return {}
    }
  }

  function setLabelColor(label: string, color: string) {
    labelColorsMap.value = { ...labelColorsMap.value, [label]: color }
  }

  function getLabelColor(label: string): string {
    if (labelColorsMap.value[label]) return labelColorsMap.value[label]
    const idx = chartLabels.value.indexOf(label)
    const colors = autoSliceColors(baseColor.value, chartLabels.value.length)
    return idx >= 0 ? (colors[idx] ?? baseColor.value) : baseColor.value
  }

  return {
    labelColorsMap,
    parseLabelColors,
    setLabelColor,
    getLabelColor,
  }
}
