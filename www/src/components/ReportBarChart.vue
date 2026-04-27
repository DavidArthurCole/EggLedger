<template>
  <div
    class="w-full h-full grid gap-x-2 gap-y-1 overflow-y-auto"
    style="grid-template-columns: max-content 1fr max-content; align-content: start;"
  >
    <template v-for="(pair, i) in sortedPairs" :key="i">
      <span class="text-gray-400 text-xs text-right self-center leading-tight" style="max-width: 9rem;">{{ pair.label }}</span>
      <div class="bg-gray-800 rounded-sm h-4 min-w-0 self-center">
        <div
          class="h-4 rounded-sm"
          :style="{ width: barWidth(pair.value) + '%', backgroundColor: barColor(pair.label) }"
          style="cursor: pointer;"
          @mouseenter="(e) => showTooltip(e, [pair.label, formatValue(pair.value) + (unitLabel ? ' ' + unitLabel : '')])"
          @mousemove="moveTooltip"
          @mouseleave="hideTooltip"
        />
      </div>
      <span class="text-gray-300 font-mono text-xs text-right self-center">{{ formatValue(pair.value) }}</span>
    </template>
    <div v-if="unitLabel" class="col-span-3 text-xs text-gray-600 text-right mt-0.5">{{ unitLabel }}</div>

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
import { computed } from 'vue'
import type { ReportResult } from '../types/bridge'
import { useChartTooltip } from '../composables/useChartTooltip'

const props = defineProps<{
  result: ReportResult
  color: string
  unitLabel?: string
  labelColors?: string
  chartType?: string
}>()

const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()

const parsedLabelColors = computed((): Record<string, string> => {
  if (!props.labelColors) return {}
  try {
    return JSON.parse(props.labelColors) as Record<string, string>
  } catch {
    return {}
  }
})

function barColor(label: string): string {
  return parsedLabelColors.value[label] ?? props.color
}

const displayValues = computed(() =>
  props.result.isFloat ? (props.result.floatValues ?? []) : props.result.values,
)

const maxVal = computed(() => Math.max(...displayValues.value.map(Number), 1))

const sortedPairs = computed(() => {
  const pairs = props.result.labels.map((label, i) => ({
    label,
    value: displayValues.value[i] ?? 0,
  }))
  if (props.chartType === 'bar_desc') return [...pairs].sort((a, b) => Number(b.value) - Number(a.value))
  if (props.chartType === 'bar_asc') return [...pairs].sort((a, b) => Number(a.value) - Number(b.value))
  return pairs
})

function barWidth(val: number): number {
  return Math.round((Number(val) / maxVal.value) * 100)
}

function formatValue(val: number): string {
  if (props.result.isFloat) return Number(val).toFixed(2)
  return String(val)
}
</script>
