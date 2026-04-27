<template>
  <div ref="containerRef" class="w-full h-full">
    <svg :viewBox="`0 0 ${vw} ${vh}`" class="w-full h-full" preserveAspectRatio="xMidYMid meet" overflow="visible">
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
        :opacity="segmentOpacity(i)"
        style="cursor: pointer; transition: opacity 0.1s;"
        @mouseenter="(e) => { hoveredIndex = i; showTooltip(e, [seg.shortLabel, String(seg.value) + ' (' + seg.pct.toFixed(1) + '%)']) }"
        @mousemove="moveTooltip"
        @mouseleave="() => { hoveredIndex = null; hideTooltip() }"
      />
      <!-- Connector lines and labels -->
      <g v-for="(seg, i) in segments" :key="'lbl-' + i">
        <line
          :x1="seg.innerX"
          :y1="seg.innerY"
          :x2="seg.elbowX"
          :y2="seg.labelY"
          :stroke="`url(#lg-${i})`"
          :stroke-width="strokeWidth"
        />
        <line
          :x1="seg.elbowX"
          :y1="seg.labelY"
          :x2="seg.labelX"
          :y2="seg.labelY"
          :stroke="`url(#lg-${i})`"
          :stroke-width="strokeWidth"
        />
        <text
          :x="seg.textX"
          :y="seg.labelY"
          :text-anchor="seg.anchor"
          dominant-baseline="middle"
          :font-size="fontSize"
          fill="#9ca3af"
        >{{ seg.shortLabel }}</text>
        <text
          :x="seg.textX"
          :y="seg.labelY + pctYOffset"
          :text-anchor="seg.anchor"
          dominant-baseline="middle"
          :font-size="fontSizePct"
          fill="#6b7280"
        >{{ seg.pct.toFixed(1) }}%</text>
      </g>
    </svg>
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

const props = defineProps<{
  result: ReportResult
  color: string
  /** JSON-encoded Record<string, string> mapping label to hex color */
  labelColors?: string
}>()

const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()
const hoveredIndex = ref<number | null>(null)

const containerRef = ref<HTMLDivElement | null>(null)
const containerSize = ref({ w: 0, h: 0 })
let resizeObserver: ResizeObserver | null = null

onMounted(() => {
  if (!containerRef.value) return
  const rect = containerRef.value.getBoundingClientRect()
  containerSize.value = { w: rect.width, h: rect.height }
  resizeObserver = new ResizeObserver(entries => {
    const entry = entries[0]
    if (entry) containerSize.value = { w: entry.contentRect.width, h: entry.contentRect.height }
  })
  resizeObserver.observe(containerRef.value)
})

onUnmounted(() => { resizeObserver?.disconnect() })

const vw = computed(() => containerSize.value.w || 260)
const vh = computed(() => containerSize.value.h || 220)
const cx = computed(() => vw.value / 2)
const cy = computed(() => vh.value / 2)

// All geometry scales from r. Cap prevents label overflow on very large cards.
const r = computed(() => Math.min(vw.value, vh.value) * 0.3)
const elbowR = computed(() => r.value * 1.291)
const labelOffset = computed(() => r.value * 0.182)
const textXOff = computed(() => r.value * 0.027)
const fontSize = computed(() => r.value * 0.091)
const fontSizePct = computed(() => r.value * 0.082)
const pctYOffset = computed(() => r.value * 0.109)
const strokeWidth = computed(() => r.value * 0.0145)

const MAX_SEGMENTS = 10
const minLabelSpacing = computed(() => r.value * 0.255)
const labelClamp = computed(() => r.value * 0.145)

function segmentOpacity(i: number): number {
  if (hoveredIndex.value === null) return 1
  return i === hoveredIndex.value ? 1 : 0.5
}

function hexToHsl(hex: string): [number, number, number] {
  const rc = Number.parseInt(hex.slice(1, 3), 16) / 255
  const gc = Number.parseInt(hex.slice(3, 5), 16) / 255
  const bc = Number.parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(rc, gc, bc)
  const min = Math.min(rc, gc, bc)
  const l = (max + min) / 2
  const d = max - min
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1))
  let h = 0
  if (d !== 0) {
    switch (max) {
      case rc: h = ((gc - bc) / d + 6) % 6; break
      case gc: h = (bc - rc) / d + 2; break
      default: h = (rc - gc) / d + 4
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

function segmentColors(baseColor: string, count: number): string[] {
  const [h, s, l] = hexToHsl(baseColor)
  return Array.from({ length: count }, (_, i) =>
    hslToHex(((h + (i * 360) / count) % 360), s, l),
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

const segments = computed(() => {
  const cxVal = cx.value
  const cyVal = cy.value
  const vhVal = vh.value
  const rVal = r.value
  const elbowRVal = elbowR.value
  const labelOffsetVal = labelOffset.value
  const textXOffVal = textXOff.value
  const minSpacing = minLabelSpacing.value
  const clamp = labelClamp.value

  function spreadLabelPositions(rawPositions: number[]): number[] {
    if (rawPositions.length <= 1) return [...rawPositions]
    const adjusted = [...rawPositions]
    const n = adjusted.length
    // Forward pass: clamp to top boundary, then push each label below the previous
    for (let i = 0; i < n; i++) {
      const floor = i === 0 ? clamp : adjusted[i - 1] + minSpacing
      if (adjusted[i] < floor) adjusted[i] = floor
    }
    // Backward pass: clamp to bottom boundary, then push each label above the next
    for (let i = n - 1; i >= 0; i--) {
      const ceiling = i === n - 1 ? vhVal - clamp : adjusted[i + 1] - minSpacing
      if (adjusted[i] > ceiling) adjusted[i] = ceiling
    }
    return adjusted
  }

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
    const sx = cxVal + Math.cos(startAngle) * rVal
    const sy = cyVal + Math.sin(startAngle) * rVal
    const ex = cxVal + Math.cos(endAngle) * rVal
    const ey = cyVal + Math.sin(endAngle) * rVal
    const large = endAngle - startAngle > Math.PI ? 1 : 0
    return `M ${cxVal} ${cyVal} L ${sx} ${sy} A ${rVal} ${rVal} 0 ${large} 1 ${ex} ${ey} Z`
  }

  let angleOffset = -Math.PI / 2
  const raw = items.map((item, i) => {
    const sweep = (item.value / total) * 2 * Math.PI
    const startAngle = angleOffset
    const endAngle = angleOffset + sweep
    angleOffset = endAngle
    const midAngle = (startAngle + endAngle) / 2

    const innerX = cxVal + Math.cos(midAngle) * rVal
    const innerY = cyVal + Math.sin(midAngle) * rVal
    const elbowX = cxVal + Math.cos(midAngle) * elbowRVal
    const rawElbowY = cyVal + Math.sin(midAngle) * elbowRVal
    const isRight = elbowX >= cxVal
    const labelX = isRight ? elbowX + labelOffsetVal : elbowX - labelOffsetVal
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
      textX: isRight ? labelX + textXOffVal : labelX - textXOffVal,
      anchor: isRight ? 'start' : 'end',
      isRight,
    }
  })

  const leftIndices = raw.map((_, i) => i).filter(i => raw[i].elbowX < cxVal)
  const rightIndices = raw.map((_, i) => i).filter(i => raw[i].elbowX >= cxVal)

  const leftOrder = [...leftIndices].sort((a, b) => raw[a].rawElbowY - raw[b].rawElbowY)
  const rightOrder = [...rightIndices].sort((a, b) => raw[a].rawElbowY - raw[b].rawElbowY)

  const leftSpread = spreadLabelPositions(leftOrder.map(i => raw[i].rawElbowY))
  const rightSpread = spreadLabelPositions(rightOrder.map(i => raw[i].rawElbowY))

  const labelYMap: Record<number, number> = {}
  leftOrder.forEach((segIdx, sortPos) => {
    labelYMap[segIdx] = Math.max(clamp, Math.min(vhVal - clamp, leftSpread[sortPos]))
  })
  rightOrder.forEach((segIdx, sortPos) => {
    labelYMap[segIdx] = Math.max(clamp, Math.min(vhVal - clamp, rightSpread[sortPos]))
  })

  return raw.map((seg, i) => ({
    ...seg,
    labelY: labelYMap[i] ?? seg.rawElbowY,
  }))
})
</script>
