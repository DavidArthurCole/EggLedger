<template>
  <div class="bg-dark rounded-lg border border-gray-700 flex flex-col h-full overflow-hidden" :class="{ 'border-indigo-700/50': editMode }">
    <!-- Centered drag handle (edit mode only) -->
    <div
      v-if="editMode"
      class="flex justify-center items-center py-1 cursor-grab active:cursor-grabbing select-none border-b border-gray-700/40"
      title="Drag to reorder"
    >
      <svg class="w-6 h-3 text-gray-500" viewBox="0 0 24 12" fill="currentColor">
        <circle cx="4" cy="3" r="1.5" />
        <circle cx="12" cy="3" r="1.5" />
        <circle cx="20" cy="3" r="1.5" />
        <circle cx="4" cy="9" r="1.5" />
        <circle cx="12" cy="9" r="1.5" />
        <circle cx="20" cy="9" r="1.5" />
      </svg>
    </div>

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
          <button
            type="button"
            class="text-gray-400 hover:text-gray-200 px-1 text-xs leading-none"
            title="Duplicate report"
            @click="$emit('copy')"
          >Copy</button>
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
          <select
            v-if="groups.length > 0"
            class="text-xs bg-darker border border-gray-700 rounded px-1 py-0.5 text-gray-500 focus:outline-none max-w-24"
            :value="def.groupId"
            @change="$emit('setGroup', ($event.target as HTMLSelectElement).value)"
          >
            <option value="">No group</option>
            <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
          </select>
        </template>
      </div>
    </div>

    <!-- Visualization area -->
    <div class="flex-1 min-h-0 p-2">
      <div v-if="running" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-500 animate-pulse">Running...</span>
      </div>
      <div v-else-if="filteredResult && filteredResult.labels?.length > 0">
        <ReportPieChart v-if="def.displayMode === 'pie'" :result="filteredResult" :color="def.color || '#6366f1'" />
        <ReportBarChart v-else-if="def.displayMode === 'bar' || def.mode === 'aggregate'" :result="filteredResult" :color="def.color || '#6366f1'" :unit-label="normalizeLabel" />
        <ReportLineChart v-else-if="def.displayMode === 'line' || def.mode === 'time_series'" :result="filteredResult" :color="def.color || '#6366f1'" :unit-label="normalizeLabel" />
        <ReportVisualGrid v-else :result="filteredResult" :unit-label="normalizeLabel" />
      </div>
      <div v-else-if="filteredResult && filteredResult.labels?.length === 0" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">No results</span>
      </div>
      <div v-else class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">Not run yet</span>
      </div>
    </div>

    <!-- Footer -->
    <div v-if="!editMode" class="flex justify-end gap-2 px-3 pb-2">
      <span v-if="filteredResult && filteredResult.labels?.length > 0" class="text-xs text-gray-600 mr-auto">
        {{ filteredResult.labels.length }} result{{ filteredResult.labels.length === 1 ? '' : 's' }}
      </span>
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
        class="tooltip-floating"
        :style="{ left: tooltipX + 'px', top: tooltipY + 'px' }"
      >
        <div class="text-xs font-semibold text-gray-300 mb-1">{{ def.weight }} - weight factors</div>
        <div
          v-for="(factor, i) in weightFactors"
          :key="i"
          class="text-xs"
          :class="factor.colorClass"
        >{{ factor.label }}</div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ReportDefinition, ReportFilterCondition, ReportResult, ReportGroup } from '../types/bridge'
import ReportBarChart from './ReportBarChart.vue'
import ReportLineChart from './ReportLineChart.vue'
import ReportPieChart from './ReportPieChart.vue'
import ReportVisualGrid from './ReportVisualGrid.vue'

const props = defineProps<{
  def: ReportDefinition
  result: ReportResult | null
  editMode: boolean
  running: boolean
  groups: ReportGroup[]
}>()

defineEmits<{
  delete: []
  edit: []
  copy: []
  run: []
  export: []
  setGroup: [groupId: string]
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

interface WeightFactor {
  label: string
  colorClass: string
}

function checkHasDateFilter(): boolean {
  const check = (c: ReportFilterCondition) => c.topLevel === 'launchDT' || c.topLevel === 'returnDT'
  return (
    props.def.filters.and.some(check) ||
    props.def.filters.or.some(g => g.some(check))
  )
}

function checkHasArtifactScopeFilter(): boolean {
  const check = (c: ReportFilterCondition) => c.topLevel.startsWith('artifact_')
  return (
    props.def.filters.and.some(check) ||
    props.def.filters.or.some(g => g.some(check))
  )
}

function customBucketDays(): number {
  const n = props.def.customBucketN
  switch (props.def.customBucketUnit) {
    case 'day': return n
    case 'week': return n * 7
    case 'month': return n * 30
    default: return n
  }
}

const weightFactors = computed((): WeightFactor[] => {
  const factors: WeightFactor[] = []
  const isTimeSeries = props.def.mode === 'time_series'

  factors.push({
    label: isTimeSeries ? 'Mode: Time series (row scan)' : 'Mode: Aggregate (indexed)',
    colorClass: isTimeSeries ? 'text-yellow-400' : 'text-green-400',
  })

  if (isTimeSeries) {
    if (props.def.timeBucket === 'custom') {
      const days = customBucketDays()
      factors.push({
        label: `Bucket window: ${days}d (${days > 90 ? 'large' : 'moderate'})`,
        colorClass: days > 90 ? 'text-red-400' : 'text-yellow-400',
      })
    }
    const hasDate = checkHasDateFilter()
    factors.push({
      label: hasDate ? 'Date filter: Present (limits scan)' : 'Date filter: None (full scan)',
      colorClass: hasDate ? 'text-green-400' : 'text-red-400',
    })
  } else {
    const hasArtifact = checkHasArtifactScopeFilter()
    factors.push({
      label: hasArtifact ? 'Artifact scope filter: Active (join required)' : 'Artifact scope filter: None',
      colorClass: hasArtifact ? 'text-yellow-400' : 'text-green-400',
    })
  }

  return factors
})

const normalizeLabel = computed(() => {
  if (props.def.normalizeBy === 'launches') return 'per launch'
  if (props.def.normalizeBy === 'airtime') return 'per flight hour'
  return ''
})

const filteredResult = computed(() => {
  if (!props.result) return null
  const op = props.def.valueFilterOp
  const threshold = props.def.valueFilterThreshold
  const isFloat = props.result.isFloat
  const rawValues = isFloat ? (props.result.floatValues ?? []) : props.result.values
  if (!op) return props.result
  const pairs = props.result.labels.map((label, i) => ({
    label,
    value: rawValues[i] ?? 0,
  }))
  let kept: typeof pairs
  if (op === '>') kept = pairs.filter(p => p.value > threshold)
  else if (op === '<') kept = pairs.filter(p => p.value < threshold)
  else if (op === '>=') kept = pairs.filter(p => p.value >= threshold)
  else if (op === '<=') kept = pairs.filter(p => p.value <= threshold)
  else if (op === '=') kept = pairs.filter(p => p.value === threshold)
  else kept = pairs
  if (isFloat) {
    return {
      labels: kept.map(p => p.label),
      values: [] as number[],
      floatValues: kept.map(p => p.value),
      isFloat: true,
      weight: props.result.weight,
    }
  }
  return {
    labels: kept.map(p => p.label),
    values: kept.map(p => p.value),
    floatValues: [] as number[],
    isFloat: false,
    weight: props.result.weight,
  }
})
</script>
