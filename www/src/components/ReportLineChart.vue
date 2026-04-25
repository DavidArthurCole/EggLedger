<template>
  <div class="w-full h-full flex flex-col overflow-hidden">
    <div class="flex-1 overflow-x-auto min-h-0">
      <div class="flex h-full gap-px">
        <div
          v-for="(label, i) in result.labels"
          :key="i"
          class="flex-1 min-w-4 flex flex-col"
          :title="`${label}: ${formatValue(displayValues[i])}`"
        >
          <div class="flex-1 flex flex-col">
            <div class="flex-shrink-0 h-4 flex justify-center items-end">
              <span
                v-if="Number(displayValues[i]) > 0"
                class="leading-none text-gray-400 pointer-events-none"
                style="font-size: 8px;"
              >{{ formatValue(displayValues[i]) }}</span>
            </div>
            <div class="flex-1 flex flex-col justify-end">
              <div
                class="w-full rounded-t-sm"
                :style="{ height: barHeight(displayValues[i]) + '%', backgroundColor: color }"
              />
            </div>
          </div>
          <div class="flex justify-center items-start overflow-hidden flex-shrink-0 pt-1" style="height: 3rem;">
            <span
              class="text-gray-500 text-center break-words w-full"
              style="font-size: 9px; line-height: 1.2; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden;"
            >{{ label }}</span>
          </div>
        </div>
      </div>
    </div>
    <div v-if="unitLabel" class="text-xs text-gray-600 text-right flex-shrink-0 mt-0.5">{{ unitLabel }}</div>
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
