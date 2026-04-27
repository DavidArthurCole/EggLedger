<template>
  <div class="h-full w-full overflow-auto pr-1">
    <table class="text-xs w-full h-full border-collapse table-fixed">
      <thead>
        <tr>
          <th class="px-1 py-0.5 text-left font-normal w-20 text-gray-600"></th>
          <th
            v-for="col in result.colLabels"
            :key="col"
            class="px-1 py-0.5 text-center font-normal text-gray-400 whitespace-nowrap"
            :title="col"
          >{{ col }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, rIdx) in result.rowLabels" :key="row">
          <td class="px-1 py-0.5 text-right pr-2 truncate text-gray-400" :title="row">{{ row }}</td>
          <td
            v-for="(col, cIdx) in result.colLabels"
            :key="col"
            class="text-center px-1 py-0.5"
            :style="cellStyle(rIdx, cIdx)"
            style="cursor: pointer;"
            @mouseenter="(e) => { hoveredCell = [rIdx, cIdx]; showTooltip(e, [col + ' ' + row, displayValue(rIdx, cIdx)]) }"
            @mousemove="moveTooltip"
            @mouseleave="() => { hoveredCell = null; hideTooltip() }"
          >{{ displayValue(rIdx, cIdx) }}</td>
        </tr>
      </tbody>
    </table>
  </div>
  <Teleport to="body">
    <div
      v-if="tooltip.visible"
      class="tooltip-floating"
      :style="{ left: tooltip.x + 'px', top: tooltip.y + 'px' }"
    >
      <div
        v-for="(line, i) in tooltip.lines"
        :key="i"
        class="text-xs"
        :class="i === 0 ? 'text-gray-200 font-medium' : 'text-gray-400'"
      >{{ line }}</div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ReportResult } from '../types/bridge'
import { useChartTooltip } from '../composables/useChartTooltip'

const props = defineProps<{
  result: ReportResult
  color: string
  normalizeBy?: string
  unfilledColor?: string
}>()

const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()
const hoveredCell = ref<[number, number] | null>(null)

const isPct = computed(() =>
  props.normalizeBy === 'row_pct' || props.normalizeBy === 'col_pct' || props.normalizeBy === 'global_pct',
)

const safeColor = computed(() =>
  props.color.startsWith('#') && props.color.length === 7 ? props.color : '#6366f1',
)

const globalMax = computed(() => {
  let max = 0
  for (const v of props.result.matrixValues ?? []) {
    if (v > max) max = v
  }
  return max
})

function idx(rIdx: number, cIdx: number): number {
  return rIdx * (props.result.colLabels?.length ?? 0) + cIdx
}

function hexToRgb(hex: string): [number, number, number] {
  return [
    Number.parseInt(hex.slice(1, 3), 16),
    Number.parseInt(hex.slice(3, 5), 16),
    Number.parseInt(hex.slice(5, 7), 16),
  ]
}

const safeUnfilledColor = computed(() => {
  const c = props.unfilledColor ?? ''
  return c.startsWith('#') && c.length === 7 ? c : '#1f2937'
})

function displayValue(rIdx: number, cIdx: number): string {
  const v = props.result.matrixValues[idx(rIdx, cIdx)] ?? 0
  if (v === 0) return '0'
  if (isPct.value) return `${v.toFixed(1)}%`
  return v % 1 === 0 ? String(v) : v.toFixed(2)
}

function cellStyle(rIdx: number, cIdx: number): Record<string, string> {
  const v = props.result.matrixValues[idx(rIdx, cIdx)] ?? 0
  const hovered = hoveredCell.value !== null && hoveredCell.value[0] === rIdx && hoveredCell.value[1] === cIdx
  if (v === 0) {
    return {
      backgroundColor: safeUnfilledColor.value,
      color: '#4b5563',
      filter: hovered ? 'brightness(1.4)' : '',
    }
  }
  const max = globalMax.value
  let intensity: number
  if (isPct.value) {
    intensity = v / 100
  } else {
    intensity = max > 0 ? v / max : 0
  }
  intensity = Math.max(0.12, intensity)
  const [fr, fg, fb] = hexToRgb(safeColor.value)
  const [br, bg, bb] = hexToRgb(safeUnfilledColor.value)
  const r = Math.round(br + (fr - br) * intensity)
  const g = Math.round(bg + (fg - bg) * intensity)
  const b = Math.round(bb + (fb - bb) * intensity)
  return {
    backgroundColor: `rgb(${r}, ${g}, ${b})`,
    color: intensity > 0.55 ? '#f3f4f6' : '#9ca3af',
    filter: hovered ? 'brightness(1.4)' : '',
  }
}
</script>
