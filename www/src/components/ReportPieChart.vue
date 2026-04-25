<template>
  <div class="flex flex-col items-center gap-2 p-1">
    <svg viewBox="0 0 100 100" class="w-full max-w-32 max-h-32" style="transform: rotate(-90deg)">
      <circle
        v-for="(seg, i) in segments"
        :key="i"
        cx="50"
        cy="50"
        r="40"
        fill="none"
        :stroke="seg.color"
        stroke-width="18"
        :stroke-dasharray="`${seg.arc} ${circumference}`"
        :stroke-dashoffset="`${circumference - seg.offset}`"
      />
    </svg>
    <div class="w-full flex flex-col gap-0.5 overflow-y-auto max-h-24">
      <div
        v-for="(seg, i) in segments"
        :key="i"
        class="flex items-center gap-1.5 text-xs text-gray-400"
      >
        <span class="w-2 h-2 rounded-full flex-shrink-0" :style="{ backgroundColor: seg.color }" />
        <span class="truncate flex-1">{{ seg.label }}</span>
        <span class="text-gray-500 flex-shrink-0">{{ seg.pct.toFixed(1) }}%</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ReportResult } from '../types/bridge'

const props = defineProps<{
  result: ReportResult
  color: string
}>()

const circumference = 2 * Math.PI * 40

const MAX_SEGMENTS = 12

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

  const colors = segmentColors(props.color, items.length)
  let offset = 0
  return items.map((item, i) => {
    const arc = (item.value / total) * circumference
    const seg = {
      label: item.label,
      value: item.value,
      arc,
      offset,
      color: colors[i],
      pct: (item.value / total) * 100,
    }
    offset += arc
    return seg
  })
})
</script>
