<template>
  <div ref="containerRef" class="h-full w-full flex flex-col gap-1">
    <div class="flex-1 min-h-0">
      <svg
        v-if="seriesCount >= 1 && bucketCount >= 2"
        ref="svgRef"
        :viewBox="`0 0 ${W} ${H}`"
        class="w-full h-full overflow-visible"
        preserveAspectRatio="xMidYMid meet"
        @mousemove="onMouseMove"
        @mouseleave="onMouseLeave"
      >
        <template v-if="globalMax > 0">
          <g v-for="tick in yTicks" :key="tick.y">
            <line
              :x1="PAD_LEFT"
              :y1="tick.y"
              :x2="W - PAD_RIGHT"
              :y2="tick.y"
              stroke="#374151"
              stroke-dasharray="3 3"
              stroke-width="0.5"
            />
            <text
              :x="PAD_LEFT - 4"
              :y="tick.y"
              text-anchor="end"
              dominant-baseline="middle"
              style="font-size: 10px; fill: #6b7280;"
            >{{ tick.label }}</text>
          </g>
        </template>

        <polyline
          v-for="(series, sIdx) in allSeriesPoints"
          :key="sIdx"
          :points="series.map(p => `${p.x},${p.y}`).join(' ')"
          fill="none"
          :stroke="seriesColors[sIdx]"
          stroke-width="1.5"
          stroke-linejoin="round"
          stroke-linecap="round"
        />

        <g v-for="(p, i) in labeledPoints" :key="i">
          <text
            :transform="`translate(${p.x}, ${H - PAD_BOTTOM + 4}) rotate(-45)`"
            text-anchor="end"
            style="font-size: 10px; fill: #6b7280;"
          >{{ formatXLabel(p.label) }}</text>
        </g>

        <template v-if="activeXIdx !== null">
          <line
            :x1="activeXPos"
            :y1="PAD_TOP"
            :x2="activeXPos"
            :y2="H - PAD_BOTTOM"
            stroke="#6b7280"
            stroke-width="0.5"
            stroke-dasharray="2 2"
          />
          <circle
            v-for="(series, sIdx) in allSeriesPoints"
            :key="sIdx"
            :cx="series[activeXIdx ?? 0]?.x ?? 0"
            :cy="series[activeXIdx ?? 0]?.y ?? 0"
            r="3"
            fill="white"
            :stroke="seriesColors[sIdx]"
            stroke-width="1.5"
          />
        </template>
      </svg>
      <div v-else-if="bucketCount < 2" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">Not enough data for a line chart</span>
      </div>
      <div v-else class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">No series data</span>
      </div>
    </div>

    <div class="flex flex-wrap gap-x-3 gap-y-0.5 px-1 flex-shrink-0">
      <div
        v-for="(col, sIdx) in result.colLabels"
        :key="col"
        class="flex items-center gap-1"
      >
        <div class="w-2 h-2 rounded-sm shrink-0" :style="{ backgroundColor: seriesColors[sIdx] }" />
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
import { computed, onMounted, onUnmounted, ref } from 'vue'
import type { ReportResult } from '../types/bridge'
import { useChartTooltip } from '../composables/useChartTooltip'

const PAD_LEFT = 36
const PAD_RIGHT = 8
const PAD_TOP = 10
const PAD_BOTTOM = 60

const props = defineProps<{
  result: ReportResult
  color: string
  labelColors?: string
  normalizeBy?: string
}>()

const containerRef = ref<HTMLDivElement | null>(null)
const containerSize = ref({ w: 300, h: 130 })
let resizeObs: ResizeObserver | null = null

onMounted(() => {
  if (!containerRef.value) return
  const rect = containerRef.value.getBoundingClientRect()
  containerSize.value = { w: rect.width || 300, h: rect.height || 130 }
  resizeObs = new ResizeObserver(entries => {
    const e = entries[0]
    if (e) containerSize.value = { w: e.contentRect.width, h: e.contentRect.height }
  })
  resizeObs.observe(containerRef.value)
})

onUnmounted(() => { resizeObs?.disconnect() })

const W = computed(() => containerSize.value.w || 300)
const H = computed(() => containerSize.value.h || 130)

const svgRef = ref<SVGSVGElement | null>(null)
const activeXIdx = ref<number | null>(null)
const activeXPos = ref(0)
const { tooltip, showTooltip, hideTooltip } = useChartTooltip()

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

function formatXLabel(s: string): string {
  const ymMatch = s.match(/^(\d{4})-(\d{2})$/)
  if (ymMatch) {
    const n = Number.parseInt(ymMatch[2], 10)
    const yr = ymMatch[1].slice(2)
    if (n >= 1 && n <= 12) return `${MONTHS[n - 1]} '${yr}`
    return `W${ymMatch[2]} '${yr}`
  }
  const ymdMatch = s.match(/^(\d{4})-(\d{2})-(\d{2})$/)
  if (ymdMatch) {
    const m = Number.parseInt(ymdMatch[2], 10)
    const d = Number.parseInt(ymdMatch[3], 10)
    return `${d} ${MONTHS[m - 1]}`
  }
  return s.length > 10 ? s.slice(0, 9) + '...' : s
}

const isPct = computed(() =>
  props.normalizeBy === 'row_pct' || props.normalizeBy === 'col_pct' || props.normalizeBy === 'global_pct',
)

function formatValue(v: number): string {
  if (isPct.value) return `${v.toFixed(1)}%`
  return v % 1 === 0 ? String(v) : v.toFixed(2)
}

const bucketCount = computed(() => props.result.rowLabels?.length ?? 0)
const seriesCount = computed(() => props.result.colLabels?.length ?? 0)

const allSeriesData = computed(() => {
  const nC = seriesCount.value
  const nR = bucketCount.value
  const matrix = props.result.matrixValues ?? []
  return Array.from({ length: nC }, (_, sIdx) =>
    Array.from({ length: nR }, (__, rIdx) => matrix[rIdx * nC + sIdx] ?? 0),
  )
})

const globalMax = computed(() => {
  let max = 0
  for (const v of props.result.matrixValues ?? []) {
    if (v > max) max = v
  }
  return max
})

const allSeriesPoints = computed(() => {
  const nR = bucketCount.value
  if (nR < 2) return []
  const max = Math.max(globalMax.value, 1)
  const xStep = (W.value - PAD_LEFT - PAD_RIGHT) / (nR - 1)
  const yRange = H.value - PAD_TOP - PAD_BOTTOM
  return allSeriesData.value.map(seriesVals =>
    seriesVals.map((v, i) => ({
      x: PAD_LEFT + i * xStep,
      y: PAD_TOP + yRange * (1 - v / max),
    })),
  )
})

const MAX_LABELS = 12
const labeledPoints = computed(() => {
  const rowLabels = props.result.rowLabels ?? []
  const points = (allSeriesPoints.value[0] ?? []).map((p, i) => ({ ...p, label: rowLabels[i] ?? '' }))
  if (points.length <= MAX_LABELS) return points
  const step = Math.ceil(points.length / MAX_LABELS)
  return points.filter((_, i) => i % step === 0 || i === points.length - 1)
})

const yTicks = computed(() => {
  const max = globalMax.value
  if (max === 0) return []
  const yRange = H.value - PAD_TOP - PAD_BOTTOM
  return [0.33, 0.67, 1].map(frac => {
    const val = max * frac
    const y = PAD_TOP + yRange * (1 - frac)
    let label: string
    if (isPct.value) label = `${val.toFixed(0)}%`
    else if (val % 1 === 0) label = String(val)
    else label = val.toFixed(1)
    return { y, label }
  })
})

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

const parsedLabelColors = computed((): Record<string, string> => {
  if (!props.labelColors) return {}
  try {
    return JSON.parse(props.labelColors) as Record<string, string>
  } catch {
    return {}
  }
})

const seriesColors = computed(() => {
  const n = seriesCount.value
  if (n === 0) return []
  const baseHex = props.color.startsWith('#') && props.color.length === 7 ? props.color : '#6366f1'
  const [h, s] = hexToHsl(baseHex)
  const colLabels = props.result.colLabels ?? []
  const perLabel = parsedLabelColors.value
  return colLabels.map((label, i) => {
    if (perLabel[label]) return perLabel[label]
    const hue = (h + (360 / n) * i) % 360
    return `hsl(${hue.toFixed(0)}, ${s.toFixed(0)}%, 55%)`
  })
})

function onMouseMove(e: MouseEvent) {
  const svg = svgRef.value
  const nR = bucketCount.value
  if (!svg || nR === 0) return
  const rect = svg.getBoundingClientRect()
  const svgX = (e.clientX - rect.left) * (W.value / rect.width)
  const xStep = (W.value - PAD_LEFT - PAD_RIGHT) / Math.max(nR - 1, 1)
  const idx = Math.round((svgX - PAD_LEFT) / xStep)
  const clampedIdx = Math.max(0, Math.min(nR - 1, idx))
  activeXIdx.value = clampedIdx
  activeXPos.value = PAD_LEFT + clampedIdx * xStep

  const bucket = (props.result.rowLabels ?? [])[clampedIdx] ?? ''
  const colLabels = props.result.colLabels ?? []
  const lines = [formatXLabel(bucket), ...colLabels.flatMap((col, sIdx) => {
    const v = allSeriesData.value[sIdx]?.[clampedIdx] ?? 0
    if (v === 0) return []
    return [`${col}: ${formatValue(v)}`]
  })]
  showTooltip(e, lines)
}

function onMouseLeave() {
  activeXIdx.value = null
  hideTooltip()
}
</script>
