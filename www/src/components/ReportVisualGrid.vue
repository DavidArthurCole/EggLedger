<template>
  <div class="w-full h-full overflow-y-auto">
    <table class="w-full text-xs">
      <tbody>
        <tr
          v-for="(label, i) in result.labels"
          :key="i"
          class="border-b border-gray-800"
        >
          <td class="py-0.5 pr-2 text-gray-400 truncate max-w-xs">{{ label }}</td>
          <td class="py-0.5 text-right font-mono text-gray-300">{{ formatValue(i) }}</td>
        </tr>
      </tbody>
    </table>
    <div v-if="unitLabel" class="text-xs text-gray-600 text-right mt-0.5">{{ unitLabel }}</div>
  </div>
</template>

<script setup lang="ts">
import type { ReportResult } from '../types/bridge'

const props = defineProps<{
  result: ReportResult
  unitLabel?: string
}>()

function formatValue(i: number): string {
  if (props.result.isFloat) {
    const v = props.result.floatValues?.[i] ?? 0
    return Number(v).toFixed(2)
  }
  return String(props.result.values[i])
}
</script>
