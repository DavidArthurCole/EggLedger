<template>
  <div class="h-full overflow-auto">
    <table class="text-xs w-full border-collapse">
      <thead>
        <tr>
          <th class="px-1 py-0.5 text-left font-normal min-w-20 text-gray-600"></th>
          <th
            v-for="col in result.colLabels"
            :key="col"
            class="px-1 py-0.5 text-center font-normal min-w-14 text-gray-400 whitespace-nowrap"
            :title="col"
          >{{ col }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, rIdx) in result.rowLabels" :key="row">
          <td class="px-1 py-0.5 text-right pr-2 whitespace-nowrap text-gray-400">{{ row }}</td>
          <td
            v-for="(col, cIdx) in result.colLabels"
            :key="col"
            class="text-center px-1 py-0.5"
            :style="cellStyle(rIdx, cIdx)"
            style="cursor: pointer;"
            @mouseenter="(e) => showTooltip(e, [row + ' x ' + col, String(displayValue(rIdx, cIdx))])"
            @mousemove="moveTooltip"
            @mouseleave="hideTooltip"
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
import { computed } from 'vue'
import type { ReportResult } from '../types/bridge'
import { useChartTooltip } from '../composables/useChartTooltip'

const props = defineProps<{
  result: ReportResult
  color: string
}>()

const { tooltip, showTooltip, moveTooltip, hideTooltip } = useChartTooltip()

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

function displayValue(rIdx: number, cIdx: number): string {
  const v = props.result.matrixValues[idx(rIdx, cIdx)] ?? 0
  if (v === 0) return '-'
  return v % 1 !== 0 ? v.toFixed(2) : String(v)
}

function cellStyle(rIdx: number, cIdx: number): Record<string, string> {
  const v = props.result.matrixValues[idx(rIdx, cIdx)] ?? 0
  const max = globalMax.value
  if (max === 0 || v === 0) return { color: '#4b5563' }
  const opacity = Math.max(0.1, v / max)
  const alphaHex = Math.round(opacity * 255).toString(16).padStart(2, '0')
  return {
    backgroundColor: `${safeColor.value}${alphaHex}`,
    color: opacity > 0.5 ? '#f3f4f6' : '#9ca3af',
  }
}
</script>
