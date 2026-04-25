<template>
  <div class="flex flex-col items-center gap-1 p-1 w-full h-full">
    <svg :viewBox="`0 0 ${vw} ${vh}`" class="w-full flex-1 min-h-0 overflow-visible">
      <!-- Donut segments -->
      <circle
        v-for="(seg, i) in segments"
        :key="'seg-' + i"
        :cx="cx"
        :cy="cy"
        :r="r"
        fill="none"
        :stroke="seg.color"
        :stroke-width="strokeW"
        :stroke-dasharray="`${seg.arc} ${circumference - seg.arc}`"
        :stroke-dashoffset="`${circumference - seg.offset}`"
        style="transform-origin: center; transform: rotate(-90deg)"
      />
      <!-- Connector lines and labels -->
      <g v-for="(seg, i) in segments" :key="'lbl-' + i">
        <line
          :x1="seg.innerX"
          :y1="seg.innerY"
          :x2="seg.elbowX"
          :y2="seg.elbowY"
          stroke="#6b7280"
          stroke-width="0.8"
        />
        <line
          :x1="seg.elbowX"
          :y1="seg.elbowY"
          :x2="seg.labelX"
          :y2="seg.elbowY"
          stroke="#6b7280"
          stroke-width="0.8"
        />
        <text
          :x="seg.textX"
          :y="seg.elbowY"
          :text-anchor="seg.anchor"
          dominant-baseline="middle"
          font-size="5"
          fill="#9ca3af"
        >{{ seg.shortLabel }}</text>
        <text
          :x="seg.textX"
          :y="seg.elbowY + 6"
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

const vw = 180
const vh = 120
const cx = vw / 2
const cy = vh / 2
const r = 32
const strokeW = 14
const circumference = 2 * Math.PI * r
const outerR = r + strokeW / 2 + 4
const elbowR = outerR + 8

const MAX_SEGMENTS = 10

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

function truncateLabel(label: string, maxLen = 14): string {
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
  let offset = 0
  return items.map((item, i) => {
    const arc = (item.value / total) * circumference
    const midAngle = (offset + arc / 2) / circumference * 2 * Math.PI - Math.PI / 2
    offset += arc

    const innerX = cx + Math.cos(midAngle) * outerR
    const innerY = cy + Math.sin(midAngle) * outerR
    const elbowX = cx + Math.cos(midAngle) * elbowR
    const elbowY = cy + Math.sin(midAngle) * elbowR
    const isRight = elbowX >= cx
    const labelX = isRight ? elbowX + 3 : elbowX - 3
    const color = perLabel[item.label] ?? autoColors[i]

    return {
      label: item.label,
      shortLabel: truncateLabel(item.label),
      value: item.value,
      arc,
      offset: offset - arc,
      color,
      pct: (item.value / total) * 100,
      innerX,
      innerY,
      elbowX,
      elbowY,
      labelX,
      anchor: isRight ? 'start' : 'end',
    }
  })
})
</script>
