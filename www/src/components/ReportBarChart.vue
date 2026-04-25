<template>
  <div
    class="w-full h-full grid gap-x-2 gap-y-1 overflow-y-auto"
    style="grid-template-columns: max-content 1fr max-content; align-content: start;"
  >
    <template v-for="(label, i) in result.labels" :key="i">
      <span class="text-gray-400 truncate text-xs text-right self-center" style="max-width: 8rem;">{{ label }}</span>
      <div class="bg-gray-800 rounded-sm h-4 min-w-0 self-center">
        <div
          class="h-4 rounded-sm"
          :style="{ width: barWidth(displayValues[i]) + '%', backgroundColor: color }"
        />
      </div>
      <span class="text-gray-300 font-mono text-xs text-right self-center">{{ formatValue(displayValues[i]) }}</span>
    </template>
    <div v-if="unitLabel" class="col-span-3 text-xs text-gray-600 text-right mt-0.5">{{ unitLabel }}</div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ReportResult } from '../types/bridge'

const props = defineProps<{
  result: ReportResult
  color: string
  unitLabel?: string
}>()

const displayValues = computed(() =>
  props.result.isFloat ? (props.result.floatValues ?? []) : props.result.values,
)

const maxVal = computed(() => Math.max(...displayValues.value.map(Number), 1))

function barWidth(val: number): number {
  return Math.round((Number(val) / maxVal.value) * 100)
}

function formatValue(val: number): string {
  if (props.result.isFloat) return Number(val).toFixed(2)
  return String(val)
}
</script>
