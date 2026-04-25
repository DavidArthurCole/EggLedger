<template>
  <div class="w-full h-full">
    <svg :viewBox="`0 0 ${vw} ${vh}`" class="w-full h-full" preserveAspectRatio="xMidYMid meet">
      <defs>
        <linearGradient
          v-for="(seg, i) in segments"
          :key="'g-' + i"
          :id="`lg-${i}`"
          gradientUnits="userSpaceOnUse"
          :x1="seg.innerX"
          :y1="seg.innerY"
          :x2="seg.labelX"
          :y2="seg.labelY"
        >
          <stop offset="0%" :stop-color="seg.color" />
          <stop offset="100%" stop-color="#6b7280" />
        </linearGradient>
      </defs>
      <!-- Pie segments -->
      <path
        v-for="(seg, i) in segments"
        :key="'seg-' + i"
        :d="seg.path"
        :fill="seg.color"
      />
      <!-- Connector lines and labels -->
      <g v-for="(seg, i) in segments" :key="'lbl-' + i">
        <line
          :x1="seg.innerX"
          :y1="seg.innerY"
          :x2="seg.elbowX"
          :y2="seg.labelY"
          :stroke="`url(#lg-${i})`"
          stroke-width="0.8"
        />
        <line
          :x1="seg.elbowX"
          :y1="seg.labelY"
          :x2="seg.labelX"
          :y2="seg.labelY"
          :stroke="`url(#lg-${i})`"
          stroke-width="0.8"
        />
        <text
          :x="seg.textX"
          :y="seg.labelY"
          :text-anchor="seg.anchor"
          dominant-baseline="middle"
          font-size="5"
          fill="#9ca3af"
        >{{ seg.shortLabel }}</text>
        <text
          :x="seg.textX"
          :y="seg.labelY + 6"
          :text-anchor="seg.anchor"
          dominant-baseline="middle"
          font-size="4.5"
          fill="#6b7280"
        >{{ seg.pct.toFixed(1) }}%</text>
      </g>
    </svg>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ReportResult } from '../types/bridge'

const props = defineProps<{
  result: ReportResult
  color: string
  /** JSON-encoded Record<string, string> mapping label to hex color */
  labelColors?: string
}>()

const vw = 260
const vh = 220
const cx = vw / 2
const cy = vh / 2
const r = 55
const elbowR = r + 16

const MAX_SEGMENTS = 10
const MIN_LABEL_SPACING = 14

function hexToHsl(hex: string): [number, number, number] {
  const r = Number.parseInt(hex.slice(1, 3), 16) / 255
  const g = Number.parseInt(hex.slice(3, 5), 16) / 255
  const b = Number.parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  const l = (max + min) / 2
  const d = max - min
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1))
  let h = 0
  if (d !== 0) {
    switch (max) {
      case r: h = ((g - b) / d + 6) % 6; break
      case g: h = (b - r) / d + 2; break
      default: h = (r - g) / d + 4
    }
    h *= 60
  }
  return [h, s, l]
}

function segmentColors(baseColor: string, count: number): string[] {
  const [h, s, l] = hexToHsl(baseColor)
  return Array.from({ length: count }, (_, i) =>
    `hsl(${(h + (i * 360) / count) % 360}, ${Math.round(s * 100)}%, ${Math.round(l * 100)}%)`,
  )
}

function truncateLabel(label: string, maxLen = 16): string {
  return label.length > maxLen ? label.slice(0, maxLen - 1) + '…' : label
}

const parsedLabelColors = computed((): Record<string, string> => {
  if (!props.labelColors) return {}
  try {
    return JSON.parse(props.labelColors) as Record<string, string>
  } catch {
    return {}
  }
})

function spreadLabelPositions(rawPositions: number[]): number[] {
  const adjusted = [...rawPositions]
  const n = adjusted.length
  if (n <= 1) return adjusted

  // Multiple passes to resolve all overlaps
  for (let pass = 0; pass < n; pass++) {
    let changed = false
    for (let i = 0; i < n - 1; i++) {
      const gap = adjusted[i + 1] - adjusted[i]
      if (gap < MIN_LABEL_SPACING) {
        const overlap = MIN_LABEL_SPACING - gap
        adjusted[i] -= overlap / 2
        adjusted[i + 1] += overlap / 2
        changed = true
      }
    }
    if (!changed) break
  }
  return adjusted
}

const segments = computed(() => {
  const labels = props.result.labels
  const values = props.result.isFloat ? (props.result.floatValues ?? []) : props.result.values
  const total = values.reduce((a, b) => a + Number(b), 0)
  if (total === 0) return []

  let items = labels.map((label, i) => ({ label, value: Number(values[i]) }))
  if (items.length > MAX_SEGMENTS) {
    const sorted = [...items].sort((a, b) => b.value - a.value)
    items = sorted.slice(0, MAX_SEGMENTS - 1)
    const other = sorted.slice(MAX_SEGMENTS - 1).reduce((s, x) => s + x.value, 0)
    items.push({ label: 'Other', value: other })
  }

  const autoColors = segmentColors(props.color, items.length)
  const perLabel = parsedLabelColors.value

  function slicePath(startAngle: number, endAngle: number): string {
    const sx = cx + Math.cos(startAngle) * r
    const sy = cy + Math.sin(startAngle) * r
    const ex = cx + Math.cos(endAngle) * r
    const ey = cy + Math.sin(endAngle) * r
    const large = endAngle - startAngle > Math.PI ? 1 : 0
    return `M ${cx} ${cy} L ${sx} ${sy} A ${r} ${r} 0 ${large} 1 ${ex} ${ey} Z`
  }

  let angleOffset = -Math.PI / 2
  const raw = items.map((item, i) => {
    const sweep = (item.value / total) * 2 * Math.PI
    const startAngle = angleOffset
    const endAngle = angleOffset + sweep
    angleOffset = endAngle
    const midAngle = (startAngle + endAngle) / 2

    const innerX = cx + Math.cos(midAngle) * r
    const innerY = cy + Math.sin(midAngle) * r
    const elbowX = cx + Math.cos(midAngle) * elbowR
    const rawElbowY = cy + Math.sin(midAngle) * elbowR
    const isRight = elbowX >= cx
    const labelX = isRight ? elbowX + 10 : elbowX - 10
    const color = perLabel[item.label] ?? autoColors[i]

    return {
      label: item.label,
      shortLabel: truncateLabel(item.label),
      value: item.value,
      path: slicePath(startAngle, endAngle),
      color,
      pct: (item.value / total) * 100,
      innerX,
      innerY,
      elbowX,
      rawElbowY,
      labelX,
      textX: isRight ? labelX + 1.5 : labelX - 1.5,
      anchor: isRight ? 'start' : 'end',
      isRight,
    }
  })

  // Split into left and right groups, sort each by rawElbowY
  const leftIndices = raw.map((_, i) => i).filter(i => raw[i].elbowX < cx)
  const rightIndices = raw.map((_, i) => i).filter(i => raw[i].elbowX >= cx)

  const leftOrder = [...leftIndices].sort((a, b) => raw[a].rawElbowY - raw[b].rawElbowY)
  const rightOrder = [...rightIndices].sort((a, b) => raw[a].rawElbowY - raw[b].rawElbowY)

  const leftSpread = spreadLabelPositions(leftOrder.map(i => raw[i].rawElbowY))
  const rightSpread = spreadLabelPositions(rightOrder.map(i => raw[i].rawElbowY))

  const labelYMap: Record<number, number> = {}
  leftOrder.forEach((segIdx, sortPos) => {
    labelYMap[segIdx] = Math.max(8, Math.min(vh - 8, leftSpread[sortPos]))
  })
  rightOrder.forEach((segIdx, sortPos) => {
    labelYMap[segIdx] = Math.max(8, Math.min(vh - 8, rightSpread[sortPos]))
  })

  return raw.map((seg, i) => ({
    ...seg,
    labelY: labelYMap[i] ?? seg.rawElbowY,
  }))
})
</script>
