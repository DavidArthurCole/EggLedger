<template>
  <div class="h-full flex flex-col gap-1 overflow-auto">
    <div
      v-for="(row, rIdx) in displayRows"
      :key="row"
      class="flex items-center gap-1 min-w-0 flex-1"
    >
      <span class="text-xs text-gray-400 w-24 shrink-0 text-right truncate" :title="row">{{ row }}</span>
      <div class="flex-1 flex flex-col gap-0.5">
        <template v-for="(col, cIdx) in displayCols" :key="col">
          <div
            v-if="cellRawValue(rIdx, cIdx) > 0"
            class="flex items-center gap-1"
          >
            <div
              class="h-3 rounded-sm transition-all"
              :style="{
                width: barWidth(rIdx, cIdx) + '%',
                backgroundColor: colColors[cIdx],
                minWidth: '2px',
                cursor: 'pointer',
                opacity: barOpacity(rIdx, cIdx),
              }"
              @mouseenter="(e) => onBarEnter(e, rIdx, cIdx, [col + ' ' + row, cellLabel(rIdx, cIdx)])"
              @mousemove="moveTooltip"
              @mouseleave="onBarLeave"
            />
            <span class="text-xs text-gray-500 shrink-0">{{ cellLabel(rIdx, cIdx) }}</span>
          </div>
        </template>
      </div>
    </div>
    <div class="flex flex-wrap gap-x-3 gap-y-0.5 mt-1 px-1">
      <div
        v-for="(col, cIdx) in displayCols"
        :key="col"
        class="flex items-center gap-1 select-none"
        :class="{
          'opacity-40': dragLegendFrom === cIdx,
          'cursor-grab': col !== 'Other',
        }"
        :draggable="col !== 'Other'"
        @dragstart.stop="col !== 'Other' && startLegendDrag(cIdx)"
        @dragover.prevent
        @drop.prevent="col !== 'Other' && onLegendDrop(cIdx)"
      >
        <div class="w-2 h-2 rounded-sm shrink-0" :style="{ backgroundColor: colColors[cIdx] }" />
        <span class="text-xs text-gray-400 truncate max-w-28" :title="col">{{ col }}</span>
      </div>
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
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ReportResult } from '../types/bridge'
import { useChartTooltip } from '../composables/useChartTooltip'

const MAX_COLS = 11

const props = defineProps<{
  result: ReportResult
  color: string
  normalizeBy?: string
}>()

const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()
const hoveredKey = ref<string | null>(null)
let clearTimer: ReturnType<typeof setTimeout> | null = null

const colOrder = ref<number[]>([])
const dragLegendFrom = ref<number | null>(null)

watch(
  () => props.result,
  (r) => {
    colOrder.value = Array.from({ length: r?.colLabels?.length ?? 0 }, (_, i) => i)
    dragLegendFrom.value = null
  },
  { immediate: true },
)

function startLegendDrag(displayIdx: number) {
  dragLegendFrom.value = displayIdx
}

function onLegendDrop(targetDisplayIdx: number) {
  if (dragLegendFrom.value === null || dragLegendFrom.value === targetDisplayIdx) {
    dragLegendFrom.value = null
    return
  }
  const fromIdx = dragLegendFrom.value
  const newOrder = [...colOrder.value]
  const [moved] = newOrder.splice(fromIdx, 1)
  newOrder.splice(targetDisplayIdx, 0, moved)
  colOrder.value = newOrder
  dragLegendFrom.value = null
}

function barOpacity(rIdx: number, cIdx: number): number {
  if (hoveredKey.value === null) return 1
  return `${rIdx}-${cIdx}` === hoveredKey.value ? 1 : 0.35
}

function onBarEnter(e: MouseEvent, rIdx: number, cIdx: number, tooltipLines: string[]) {
  if (clearTimer !== null) { clearTimeout(clearTimer); clearTimer = null }
  hoveredKey.value = `${rIdx}-${cIdx}`
  showTooltip(e, tooltipLines)
}

function onBarLeave() {
  clearTimer = setTimeout(() => { hoveredKey.value = null; clearTimer = null }, 120)
  hideTooltip()
}

const isPct = computed(() =>
  props.normalizeBy === 'row_pct' || props.normalizeBy === 'col_pct' || props.normalizeBy === 'global_pct',
)

const hasOtherGroup = computed(() => (props.result.colLabels?.length ?? 0) > MAX_COLS + 1)

const displayCols = computed(() => {
  const rawCols = props.result.colLabels ?? []
  const ordered = colOrder.value.map((i) => rawCols[i])
  if (ordered.length <= MAX_COLS + 1) return ordered
  return [...ordered.slice(0, MAX_COLS), 'Other']
})

const displayRows = computed(() => props.result.rowLabels ?? [])

const globalMax = computed(() => {
  let max = 0
  for (const v of props.result.matrixValues ?? []) {
    if (v > max) max = v
  }
  return max
})

function rawColCount(): number {
  return props.result.colLabels?.length ?? 0
}

function cellRawValue(rIdx: number, cDisplayIdx: number): number {
  const nCols = rawColCount()
  if (hasOtherGroup.value && cDisplayIdx >= MAX_COLS) {
    let sum = 0
    for (let c = MAX_COLS; c < colOrder.value.length; c++) {
      sum += props.result.matrixValues[rIdx * nCols + colOrder.value[c]] ?? 0
    }
    return sum
  }
  const origCIdx = colOrder.value[cDisplayIdx]
  return props.result.matrixValues[rIdx * nCols + origCIdx] ?? 0
}

function barWidth(rIdx: number, cIdx: number): number {
  if (isPct.value) return cellRawValue(rIdx, cIdx)
  const max = globalMax.value
  if (max === 0) return 0
  return (cellRawValue(rIdx, cIdx) / max) * 100
}

function cellLabel(rIdx: number, cIdx: number): string {
  const v = cellRawValue(rIdx, cIdx)
  if (isPct.value) return `${v.toFixed(1)}%`
  return v % 1 === 0 ? String(v) : v.toFixed(2)
}

function hexToHsl(hex: string): [number, number, number] {
  const r = Number.parseInt(hex.slice(1, 3), 16) / 255
  const g = Number.parseInt(hex.slice(3, 5), 16) / 255
  const b = Number.parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b)
  let h = 0, s = 0
  const l = (max + min) / 2
  if (max !== min) {
    const d = max - min
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
    if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6
    else if (max === g) h = ((b - r) / d + 2) / 6
    else h = ((r - g) / d + 4) / 6
  }
  return [h * 360, s * 100, l * 100]
}

const colColors = computed(() => {
  const totalCols = props.result.colLabels?.length ?? 0
  if (totalCols === 0) return []
  const [h, s] = hexToHsl(props.color.startsWith('#') && props.color.length === 7 ? props.color : '#6366f1')
  return displayCols.value.map((col, dIdx) => {
    if (col === 'Other') return '#6b7280'
    const origIdx = colOrder.value[dIdx]
    const hue = (h + (360 / totalCols) * origIdx) % 360
    return `hsl(${hue.toFixed(0)}, ${s.toFixed(0)}%, 55%)`
  })
})
</script>
