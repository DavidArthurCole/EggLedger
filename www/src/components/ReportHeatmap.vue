<template>
  <div class="h-full w-full overflow-auto pr-1">
    <table class="text-xs w-full h-full border-collapse table-fixed">
      <thead>
        <tr>
          <th class="px-1 py-0.5 text-left font-normal w-20 text-gray-600"></th>
          <th
            v-for="(origColIdx, dColIdx) in colOrder"
            :key="result.colLabels[origColIdx]"
            class="px-1 py-0.5 text-center font-normal text-gray-400 whitespace-nowrap select-none hover:bg-white/5 hover:text-gray-200 rounded cursor-grab"
            :class="{ 'opacity-40': dragFrom?.axis === 'col' && dragFrom?.idx === dColIdx }"
            :title="`Drag to reorder: ${result.colLabels[origColIdx]}`"
            draggable="true"
            @dragstart.stop="startDrag('col', dColIdx)"
            @dragover.prevent
            @drop.prevent="onDrop('col', dColIdx)"
          >{{ result.colLabels[origColIdx] }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(origRowIdx, dRowIdx) in rowOrder" :key="result.rowLabels[origRowIdx]">
          <td
            class="px-1 py-0.5 text-right pr-2 truncate text-gray-400 select-none hover:bg-white/5 hover:text-gray-200 cursor-ns-resize"
            :class="{ 'opacity-40': dragFrom?.axis === 'row' && dragFrom?.idx === dRowIdx }"
            :title="`Drag to reorder: ${result.rowLabels[origRowIdx]}`"
            draggable="true"
            @dragstart.stop="startDrag('row', dRowIdx)"
            @dragover.prevent
            @drop.prevent="onDrop('row', dRowIdx)"
          >{{ result.rowLabels[origRowIdx] }}</td>
          <td
            v-for="(origColIdx, dColIdx) in colOrder"
            :key="result.colLabels[origColIdx]"
            class="text-center px-1 py-0.5"
            :style="cellStyle(dRowIdx, dColIdx)"
            style="cursor: pointer;"
            @mouseenter="(e) => { hoveredCell = [dRowIdx, dColIdx]; showTooltip(e, tooltipLines(dRowIdx, dColIdx, origColIdx, origRowIdx)) }"
            @mousemove="moveTooltip"
            @mouseleave="() => { hoveredCell = null; hideTooltip() }"
          >
            <div class="leading-none">{{ displayValue(dRowIdx, dColIdx) }}</div>
            <div v-if="secondaryValues" class="leading-none opacity-70" style="font-size: 0.65rem;">{{ secondaryDisplayValue(dRowIdx, dColIdx) }}</div>
          </td>
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
import { computed, ref, watch } from 'vue'
import type { ReportResult } from '../types/bridge'
import { useChartTooltip } from '../composables/useChartTooltip'

const props = defineProps<{
  result: ReportResult
  color: string
  normalizeBy?: string
  unfilledColor?: string
  secondaryValues?: number[]
  /** When present, overrides cell background intensity using ratio semantics (1.0=community avg, 2.0=full intensity). Display values are unaffected. */
  colorValues?: number[]
  /** Per-cell mission counts; used with minSampleSize to suppress low-sample cells */
  missionCounts?: number[]
  /** Cells with fewer missions than this are shown as "—"; 0 = disabled */
  minSampleSize?: number
}>()

const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()
const hoveredCell = ref<[number, number] | null>(null)

const colOrder = ref<number[]>([])
const rowOrder = ref<number[]>([])
const dragFrom = ref<{ axis: 'col' | 'row'; idx: number } | null>(null)

watch(
  () => props.result,
  (r) => {
    colOrder.value = Array.from({ length: r?.colLabels?.length ?? 0 }, (_, i) => i)
    rowOrder.value = Array.from({ length: r?.rowLabels?.length ?? 0 }, (_, i) => i)
    dragFrom.value = null
  },
  { immediate: true },
)

function startDrag(axis: 'col' | 'row', displayIdx: number) {
  dragFrom.value = { axis, idx: displayIdx }
}

function onDrop(axis: 'col' | 'row', targetDisplayIdx: number) {
  if (!dragFrom.value || dragFrom.value.axis !== axis) {
    dragFrom.value = null
    return
  }
  const fromDisplayIdx = dragFrom.value.idx
  if (fromDisplayIdx === targetDisplayIdx) {
    dragFrom.value = null
    return
  }
  const order = axis === 'col' ? colOrder.value : rowOrder.value
  const newOrder = [...order]
  const [moved] = newOrder.splice(fromDisplayIdx, 1)
  newOrder.splice(targetDisplayIdx, 0, moved)
  if (axis === 'col') colOrder.value = newOrder
  else rowOrder.value = newOrder
  dragFrom.value = null
}

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

function idx(dRowIdx: number, dColIdx: number): number {
  const origR = rowOrder.value[dRowIdx]
  const origC = colOrder.value[dColIdx]
  return origR * (props.result.colLabels?.length ?? 0) + origC
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

function isBelowThreshold(flatIdx: number): boolean {
  const threshold = props.minSampleSize ?? 0
  if (threshold <= 0 || !props.missionCounts) return false
  return (props.missionCounts[flatIdx] ?? 0) < threshold
}

function tooltipLines(dRowIdx: number, dColIdx: number, origColIdx: number, origRowIdx: number): string[] {
  const flatIdx = idx(dRowIdx, dColIdx)
  const label = props.result.colLabels[origColIdx] + ' ' + props.result.rowLabels[origRowIdx]
  if (isBelowThreshold(flatIdx)) {
    const count = props.missionCounts?.[flatIdx] ?? 0
    return [label, `Insufficient data (n=${count})`]
  }
  return [label, displayValue(dRowIdx, dColIdx)]
}

function displayValue(dRowIdx: number, dColIdx: number): string {
  const flatIdx = idx(dRowIdx, dColIdx)
  if (isBelowThreshold(flatIdx)) return '-'
  const v = props.result.matrixValues[flatIdx] ?? 0
  if (props.normalizeBy === 'ratio') {
    if (v === 0) return '-'
    return `x${v.toFixed(2)}`
  }
  if (v === 0) return '0'
  if (isPct.value) return `${v.toFixed(1)}%`
  return v % 1 === 0 ? String(v) : v.toFixed(2)
}

function secondaryDisplayValue(dRowIdx: number, dColIdx: number): string {
  if (!props.secondaryValues) return ''
  const flatIdx = idx(dRowIdx, dColIdx)
  if (isBelowThreshold(flatIdx)) return ''
  const v = props.secondaryValues[flatIdx] ?? 0
  if (v === 0) return '0'
  if (isPct.value) return `${v.toFixed(1)}%`
  return v % 1 === 0 ? String(v) : v.toFixed(2)
}

function relativeLuminance(r: number, g: number, b: number): number {
  const toLinear = (c: number) => {
    const s = c / 255
    return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4)
  }
  return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b)
}

function cellIntensity(flatIdx: number, v: number): number {
  if (props.colorValues) {
    const cv = props.colorValues[flatIdx] ?? 0
    return Math.max(0.12, cv > 0 ? Math.min(cv / 2, 1) : 0)
  }
  if (isPct.value) return Math.max(0.12, v / 100)
  if (props.normalizeBy === 'ratio') return Math.max(0.12, Math.min(v / 2, 1))
  return Math.max(0.12, globalMax.value > 0 ? v / globalMax.value : 0)
}

function cellStyle(dRowIdx: number, dColIdx: number): Record<string, string> {
  const flatIdx = idx(dRowIdx, dColIdx)
  const v = props.result.matrixValues[flatIdx] ?? 0
  const hovered = hoveredCell.value !== null && hoveredCell.value[0] === dRowIdx && hoveredCell.value[1] === dColIdx
  if (isBelowThreshold(flatIdx)) {
    const [ur, ug, ub] = hexToRgb(safeUnfilledColor.value)
    return {
      backgroundColor: safeUnfilledColor.value,
      color: relativeLuminance(ur, ug, ub) > 0.179 ? '#1f2937' : '#6b7280',
      filter: hovered ? 'brightness(1.4)' : '',
    }
  }
  if (v === 0) {
    const [ur, ug, ub] = hexToRgb(safeUnfilledColor.value)
    return {
      backgroundColor: safeUnfilledColor.value,
      color: relativeLuminance(ur, ug, ub) > 0.179 ? '#1f2937' : '#6b7280',
      filter: hovered ? 'brightness(1.4)' : '',
    }
  }
  const intensity = cellIntensity(flatIdx, v)
  const [fr, fg, fb] = hexToRgb(safeColor.value)
  const [br, bg, bb] = hexToRgb(safeUnfilledColor.value)
  const r = Math.round(br + (fr - br) * intensity)
  const g = Math.round(bg + (fg - bg) * intensity)
  const b = Math.round(bb + (fb - bb) * intensity)
  return {
    backgroundColor: `rgb(${r}, ${g}, ${b})`,
    color: relativeLuminance(r, g, b) > 0.179 ? '#1f2937' : '#f3f4f6',
    filter: hovered ? 'brightness(1.4)' : '',
  }
}
</script>
