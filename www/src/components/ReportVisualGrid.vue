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
          <td class="py-0.5 text-right font-mono text-gray-300">{{ result.values[i] }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
import type { ReportResult } from '../types/bridge'

defineProps<{
  result: ReportResult
}>()
</script>
