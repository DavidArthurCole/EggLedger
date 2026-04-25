<template>
  <div class="w-full h-full flex flex-col gap-1 overflow-hidden">
    <div class="flex-1 flex items-end gap-px overflow-x-auto min-h-0">
      <div
        v-for="(label, i) in result.labels"
        :key="i"
        class="flex-1 min-w-3 h-full flex flex-col justify-end"
        :title="`${label}: ${formatValue(displayValues[i])}`"
      >
        <div
          class="w-full rounded-t-sm"
          :style="{ height: barHeight(displayValues[i]) + '%', backgroundColor: color }"
        />
      </div>
    </div>
    <div v-if="unitLabel" class="text-xs text-gray-600 text-right flex-shrink-0">{{ unitLabel }}</div>
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

function barHeight(val: number): number {
  const pct = Math.round((Number(val) / maxVal.value) * 100)
  return Number(val) > 0 ? Math.max(pct, 2) : 0
}

function formatValue(val: number): string {
  if (props.result.isFloat) return Number(val).toFixed(2)
  return String(val)
}
</script>
