<template>
  <div class="bg-dark rounded-lg border border-gray-700 flex flex-col h-full overflow-hidden" :class="{ 'border-indigo-700/50': editMode }">
    <!-- Header -->
    <div class="flex items-start justify-between gap-2 px-3 pt-3 pb-1.5 border-b border-gray-700/50">
      <div class="min-w-0 flex-1">
        <span class="text-sm font-medium text-gray-200 truncate block">{{ def.name }}</span>
        <span class="text-xs text-gray-500">{{ subjectLabel }} - {{ modeLabel }}</span>
        <span
          v-if="def.description"
          class="text-xs text-gray-600 truncate block mt-0.5"
          :title="def.description"
        >{{ def.description }}</span>
      </div>
      <div class="flex items-center gap-1 flex-shrink-0">
        <span
          class="text-xs px-1.5 py-0.5 rounded cursor-default"
          :class="weightClass"
          @mouseenter="onWeightHover"
          @mouseleave="showWeightTooltip = false"
        >{{ def.weight }}</span>
        <template v-if="editMode">
          <span
            class="text-gray-600 hover:text-gray-400 px-1 cursor-grab active:cursor-grabbing select-none"
            title="Drag to reorder"
          >
            <svg class="w-3 h-3" viewBox="0 0 12 16" fill="currentColor">
              <circle cx="3" cy="3" r="1.5" />
              <circle cx="9" cy="3" r="1.5" />
              <circle cx="3" cy="8" r="1.5" />
              <circle cx="9" cy="8" r="1.5" />
              <circle cx="3" cy="13" r="1.5" />
              <circle cx="9" cy="13" r="1.5" />
            </svg>
          </span>
          <button
            type="button"
            class="text-blue-400 hover:text-blue-300 px-1 text-xs leading-none"
            @click="$emit('edit')"
          >Edit</button>
          <button
            type="button"
            class="text-red-500 hover:text-red-400 px-1 text-xs leading-none"
            @click="$emit('delete')"
          >&#10005;</button>
        </template>
      </div>
    </div>

    <!-- Visualization area -->
    <div class="flex-1 min-h-0 p-2">
      <div v-if="running" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-500 animate-pulse">Running...</span>
      </div>
      <div v-else-if="result && result.labels.length > 0">
        <ReportPieChart v-if="def.displayMode === 'pie'" :result="result" :color="def.color || '#6366f1'" />
        <ReportBarChart v-else-if="def.displayMode === 'bar' || def.mode === 'aggregate'" :result="result" :color="def.color || '#6366f1'" />
        <ReportLineChart v-else-if="def.displayMode === 'line' || def.mode === 'time_series'" :result="result" :color="def.color || '#6366f1'" />
        <ReportVisualGrid v-else :result="result" />
      </div>
      <div v-else-if="result && result.labels.length === 0" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">No results</span>
      </div>
      <div v-else class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">Not run yet</span>
      </div>
    </div>

    <!-- Footer -->
    <div v-if="!editMode" class="flex justify-end gap-2 px-3 pb-2">
      <button
        type="button"
        class="text-xs text-gray-500 hover:text-gray-400 disabled:opacity-40"
        :disabled="running"
        @click="$emit('export')"
      >Export</button>
      <button
        type="button"
        class="text-xs text-indigo-400 hover:text-indigo-300 disabled:opacity-40"
        :disabled="running"
        @click="$emit('run')"
      >Run</button>
    </div>
  </div>

  <Teleport to="body">
    <Transition name="tooltip-fade">
      <div
        v-if="showWeightTooltip"
        class="tooltip-floating text-sm"
        :style="{ left: tooltipX + 'px', top: tooltipY + 'px' }"
      >
        Processing load indicator - how much data this report scans
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ReportDefinition, ReportResult } from '../types/bridge'
import ReportBarChart from './ReportBarChart.vue'
import ReportLineChart from './ReportLineChart.vue'
import ReportPieChart from './ReportPieChart.vue'
import ReportVisualGrid from './ReportVisualGrid.vue'

const props = defineProps<{
  def: ReportDefinition
  result: ReportResult | null
  editMode: boolean
  running: boolean
}>()

defineEmits<{
  delete: []
  edit: []
  run: []
  export: []
}>()

const showWeightTooltip = ref(false)
const tooltipX = ref(0)
const tooltipY = ref(0)

function onWeightHover(e: MouseEvent) {
  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
  tooltipX.value = rect.left + rect.width / 2
  tooltipY.value = rect.top
  showWeightTooltip.value = true
}

const subjectLabel = computed(() => {
  const map: Record<string, string> = { ships: 'Ships', artifacts: 'Artifacts', missions: 'Missions' }
  return map[props.def.subject] ?? props.def.subject
})

const modeLabel = computed(() => {
  return props.def.mode === 'time_series' ? 'Time series' : 'Aggregate'
})

const weightClass = computed(() => {
  switch (props.def.weight) {
    case 'HEAVY': return 'bg-red-900/40 text-red-400'
    case 'MEDIUM': return 'bg-yellow-900/40 text-yellow-400'
    default: return 'bg-green-900/40 text-green-400'
  }
})
</script>
