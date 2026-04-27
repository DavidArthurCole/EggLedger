<template>
  <div ref="containerRef" class="h-full w-full">
    <svg
      v-if="points.length >= 2"
      ref="svgRef"
      :viewBox="`0 0 ${W} ${H}`"
      class="w-full h-full overflow-visible"
      preserveAspectRatio="xMidYMid meet"
      @mousemove="onMouseMove"
      @mouseleave="onMouseLeave"
    >
      <defs>
        <linearGradient :id="gradId" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" :stop-color="color" stop-opacity="0.3" />
          <stop offset="100%" :stop-color="color" stop-opacity="0.02" />
        </linearGradient>
      </defs>

      <template v-if="maxVal > 0">
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
            style="font-size: 8px; fill: #6b7280;"
          >{{ tick.label }}</text>
        </g>
      </template>

      <path v-if="chartType !== 'line'" :d="areaPath" :fill="`url(#${gradId})`" />

      <polyline
        :points="linePoints"
        fill="none"
        :stroke="color"
        stroke-width="1.5"
        stroke-linejoin="round"
        stroke-linecap="round"
      />

      <g v-for="(p, i) in labeledPoints" :key="i">
        <text
          :transform="`translate(${p.x}, ${H - PAD_BOTTOM + 4}) rotate(-45)`"
          text-anchor="end"
          style="font-size: 8px; fill: #6b7280;"
        >{{ truncateLabel(p.label) }}</text>
      </g>

      <circle
        v-if="activePoint"
        :cx="activePoint.x"
        :cy="activePoint.y"
        r="3"
        fill="white"
        :stroke="color"
        stroke-width="1.5"
      />
    </svg>
    <div v-else class="h-full flex items-center justify-center">
      <span class="text-xs text-gray-600 italic">Not enough data for a line chart</span>
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
const PAD_BOTTOM = 42

const props = defineProps<{
  result: ReportResult
  color: string
  unitLabel?: string
  chartType?: string
}>()

const containerRef = ref<HTMLDivElement | null>(null)
const containerSize = ref({ w: 300, h: 130 })
let lineObs: ResizeObserver | null = null

onMounted(() => {
  if (!containerRef.value) return
  const rect = containerRef.value.getBoundingClientRect()
  containerSize.value = { w: rect.width || 300, h: rect.height || 130 }
  lineObs = new ResizeObserver(entries => {
    const e = entries[0]
    if (e) containerSize.value = { w: e.contentRect.width, h: e.contentRect.height }
  })
  lineObs.observe(containerRef.value)
})

onUnmounted(() => { lineObs?.disconnect() })

const W = computed(() => containerSize.value.w || 300)
const H = computed(() => containerSize.value.h || 130)

const svgRef = ref<SVGSVGElement | null>(null)
const activePoint = ref<{ x: number; y: number; label: string; value: number } | null>(null)
const gradId = `line-grad-${Math.random().toString(36).slice(2, 7)}`
const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()

function truncateLabel(s: string, max = 14): string {
  return s.length > max ? s.slice(0, max - 1) + '...' : s
}

function formatValue(v: number): string {
  if (props.result.isFloat) return v.toFixed(2)
  return String(Math.round(v))
}

const values = computed(() => {
  if (props.result.isFloat) return props.result.floatValues ?? []
  return (props.result.values ?? []).map(Number)
})

const maxVal = computed(() => Math.max(...values.value, 0))

const points = computed(() => {
  const vs = values.value
  const labels = props.result.labels ?? []
  if (vs.length < 2) return []
  const max = Math.max(...vs, 1)
  const xStep = (W.value - PAD_LEFT - PAD_RIGHT) / (vs.length - 1)
  const yRange = H.value - PAD_TOP - PAD_BOTTOM
  return vs.map((v, i) => ({
    x: PAD_LEFT + i * xStep,
    y: PAD_TOP + yRange * (1 - v / max),
    label: labels[i] ?? '',
    value: v,
  }))
})

const linePoints = computed(() =>
  points.value.map(p => `${p.x},${p.y}`).join(' '),
)

const areaPath = computed(() => {
  const ps = points.value
  if (ps.length < 2) return ''
  const baseline = PAD_TOP + (H.value - PAD_TOP - PAD_BOTTOM)
  const line = ps.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x},${p.y}`).join(' ')
  return `${line} L${ps[ps.length - 1].x},${baseline} L${ps[0].x},${baseline} Z`
})

const MAX_LABELS = 20
const labeledPoints = computed(() => {
  const ps = points.value
  if (ps.length <= MAX_LABELS) return ps
  const step = Math.ceil(ps.length / MAX_LABELS)
  return ps.filter((_, i) => i % step === 0 || i === ps.length - 1)
})

const yTicks = computed(() => {
  const max = maxVal.value
  if (max === 0) return []
  const yRange = H.value - PAD_TOP - PAD_BOTTOM
  return [0, 0.33, 0.67, 1].map(frac => {
    const val = max * frac
    const y = PAD_TOP + yRange * (1 - frac)
    const label = props.result.isFloat ? val.toFixed(1) : String(Math.round(val))
    return { y, label }
  })
})

function onMouseMove(e: MouseEvent) {
  const svg = svgRef.value
  if (!svg || points.value.length === 0) return
  const rect = svg.getBoundingClientRect()
  const svgX = (e.clientX - rect.left) * (W.value / rect.width)
  let nearest = points.value[0]
  let minDist = Math.abs(points.value[0].x - svgX)
  for (const p of points.value) {
    const d = Math.abs(p.x - svgX)
    if (d < minDist) { minDist = d; nearest = p }
  }
  activePoint.value = nearest
  showTooltip(e, [
    nearest.label,
    formatValue(nearest.value) + (props.unitLabel ? ' ' + props.unitLabel : ''),
  ])
}

function onMouseLeave() {
  activePoint.value = null
  hideTooltip()
}
</script>
