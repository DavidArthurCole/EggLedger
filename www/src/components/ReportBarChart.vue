<template>
  <div class="w-full h-full flex flex-col gap-1 overflow-y-auto">
    <div
      v-for="(label, i) in result.labels"
      :key="i"
      class="flex items-center gap-2 text-xs"
    >
      <span class="text-gray-400 truncate w-24 flex-shrink-0 text-right">{{ label }}</span>
      <div class="flex-1 bg-gray-800 rounded-sm h-4 min-w-0">
        <div
          class="h-4 rounded-sm"
          :style="{ width: barWidth(result.values[i]) + '%', backgroundColor: color }"
        />
      </div>
      <span class="text-gray-300 font-mono flex-shrink-0 w-12 text-right">{{ result.values[i] }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ReportResult } from '../types/bridge'

const props = defineProps<{
  result: ReportResult
  color: string
}>()

const maxVal = computed(() => Math.max(...props.result.values, 1))

function barWidth(val: number): number {
  return Math.round((val / maxVal.value) * 100)
}
</script>
