<template>
  <div class="relative bg-dark rounded-lg border border-gray-700 flex flex-col h-full overflow-hidden" :class="{ 'border-indigo-700/50': editMode }">
    <!-- Drag handle - absolutely positioned so it never shifts layout -->
    <div
      v-if="editMode"
      class="absolute top-1.5 left-1/2 -translate-x-1/2 z-10 cursor-grab active:cursor-grabbing select-none"
      title="Drag to reorder"
    >
      <svg class="w-6 h-3 text-gray-600" viewBox="0 0 24 12" fill="currentColor">
        <circle cx="4" cy="3" r="1.5" />
        <circle cx="12" cy="3" r="1.5" />
        <circle cx="20" cy="3" r="1.5" />
        <circle cx="4" cy="9" r="1.5" />
        <circle cx="12" cy="9" r="1.5" />
        <circle cx="20" cy="9" r="1.5" />
      </svg>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between gap-2 px-3 pb-1.5 border-b border-gray-700/50" :class="editMode ? 'pt-5' : 'pt-3'">
      <div class="min-w-0 flex-1">
        <span class="text-sm font-medium text-gray-200 truncate block">{{ def.name }}</span>
        <span class="text-xs text-gray-500">{{ subjectLabel }} - {{ modeLabel }}{{ groupByLabel ? ' - ' + groupByLabel : '' }}{{ secondaryGroupByLabel ? ' x ' + secondaryGroupByLabel : '' }}</span>
        <span
          v-if="def.description"
          class="text-xs text-gray-500 truncate block mt-0.5"
          :title="def.description"
        >{{ def.description }}</span>
      </div>
      <div class="flex items-center gap-1 flex-shrink-0">
        <button
          v-if="stale && !running"
          type="button"
          class="text-yellow-500 hover:text-yellow-400"
          title="Results are stale - click to refresh"
          @click="$emit('run')"
        >
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
        </button>
        <span
          class="text-xs px-1.5 py-0.5 rounded cursor-default"
          :class="weightClass"
          @mouseenter="onWeightHover"
          @mouseleave="showWeightTooltip = false"
        >{{ def.weight }}</span>
      </div>
    </div>

    <!-- Visualization area -->
    <div class="flex-1 min-h-0 p-2">
      <div v-if="running" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-500 animate-pulse">Running...</span>
      </div>
      <template v-else-if="result && result.is2D">
        <ReportHeatmap v-if="def.displayMode === 'heatmap'" :result="result" :color="def.color || '#6366f1'" :normalize-by="def.normalizeBy || 'none'" />
        <ReportGroupedBar v-else-if="def.displayMode === 'grouped_bar'" :result="result" :color="def.color || '#6366f1'" :normalize-by="def.normalizeBy || 'none'" />
        <ReportMultiLineChart v-else-if="def.displayMode === 'multi_line'" :result="result" :color="def.color || '#6366f1'" :label-colors="def.labelColors" :normalize-by="def.normalizeBy || 'none'" />
        <div v-else class="h-full flex items-center justify-center">
          <span class="text-xs text-gray-600 italic">Select a 2D display mode</span>
        </div>
      </template>
      <div v-else-if="filteredResult && filteredResult.labels?.length > 0 && def.displayMode === 'pie'" class="h-full">
        <ReportPieChart :result="filteredResult" :color="def.color || '#6366f1'" :label-colors="def.labelColors" />
      </div>
      <template v-else-if="filteredResult && filteredResult.labels?.length > 0">
        <ReportBarChart v-if="def.displayMode === 'bar' || (!def.displayMode && def.mode === 'aggregate')" :result="filteredResult" :color="def.color || '#6366f1'" :unit-label="normalizeLabel" :label-colors="def.labelColors" :chart-type="def.chartType" />
        <ReportLineChart v-else-if="def.displayMode === 'line' || (!def.displayMode && def.mode === 'time_series')" :result="filteredResult" :color="def.color || '#6366f1'" :unit-label="normalizeLabel" :chart-type="def.chartType" />
        <ReportVisualGrid v-else :result="filteredResult" :unit-label="normalizeLabel" />
      </template>
      <div v-else-if="filteredResult && filteredResult.labels?.length === 0" class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">No results</span>
      </div>
      <div v-else class="h-full flex items-center justify-center">
        <span class="text-xs text-gray-600 italic">Not run yet</span>
      </div>
    </div>

    <!-- Footer -->
    <div class="flex justify-end items-center gap-2 px-3 pb-2">
      <template v-if="editMode">
        <select
          v-if="groups.length > 0"
          class="text-xs bg-darker border border-gray-700 rounded pl-1 pr-6 py-0.5 text-gray-500 focus:outline-none max-w-36 mr-auto"
          :value="def.groupId"
          @change="$emit('setGroup', ($event.target as HTMLSelectElement).value)"
        >
          <option value="">No group</option>
          <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
        </select>
        <button
          type="button"
          class="text-gray-400 hover:text-gray-200 px-1 text-xs leading-none disabled:opacity-40"
          title="Duplicate report"
          :disabled="isCopying || isDeleting"
          @click="handleCopy"
        >{{ isCopying ? 'Copying...' : 'Copy' }}</button>
        <button
          type="button"
          class="text-blue-400 hover:text-blue-300 px-1 text-xs leading-none disabled:opacity-40"
          :disabled="isCopying || isDeleting"
          @click="$emit('edit')"
        >Edit</button>
        <template v-if="confirmingDelete">
          <span class="text-xs text-red-400">Delete?</span>
          <button
            type="button"
            class="text-xs text-gray-400 hover:text-gray-200 px-1 leading-none"
            @click="confirmingDelete = false"
          >Cancel</button>
          <button
            type="button"
            class="text-xs text-red-400 hover:text-red-300 font-semibold px-1 leading-none disabled:opacity-40"
            :disabled="isDeleting"
            @click="handleDelete"
          >{{ isDeleting ? 'Deleting...' : 'Yes' }}</button>
        </template>
        <button
          v-else
          type="button"
          class="text-red-500 hover:text-red-400 px-1 text-xs leading-none disabled:opacity-40"
          :disabled="isCopying || isDeleting"
          @click="confirmingDelete = true"
        >&#10005;</button>
      </template>
      <template v-else>
        <span
          v-if="filteredResult && filteredResult.labels?.length > 0 && def.displayMode !== 'pie' && def.displayMode !== 'bar' && def.displayMode !== 'line'"
          class="text-xs text-gray-500 mr-auto"
        >
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
      </template>
    </div>
  </div>

  <Teleport to="body">
    <Transition name="tooltip-fade">
      <div
        v-if="showWeightTooltip"
        ref="tooltipRef"
        class="tooltip-floating"
        :style="{ left: tooltipX + 'px', top: tooltipY + 'px', '--arrow-offset': tooltipArrowOffset + 'px' }"
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
import { computed, nextTick, ref, watch } from 'vue'
import type { ReportDefinition, ReportFilterCondition, ReportResult, ReportGroup } from '../types/bridge'
import ReportBarChart from './ReportBarChart.vue'
import ReportGroupedBar from './ReportGroupedBar.vue'
import ReportHeatmap from './ReportHeatmap.vue'
import ReportLineChart from './ReportLineChart.vue'
import ReportMultiLineChart from './ReportMultiLineChart.vue'
import ReportPieChart from './ReportPieChart.vue'
import ReportVisualGrid from './ReportVisualGrid.vue'

const props = defineProps<{
  def: ReportDefinition
  result: ReportResult | null
  editMode: boolean
  running: boolean
  groups: ReportGroup[]
  stale: boolean
}>()

const confirmingDelete = ref(false)
const isCopying = ref(false)
const isDeleting = ref(false)

watch(() => props.editMode, (val) => {
  if (!val) {
    confirmingDelete.value = false
    isCopying.value = false
    isDeleting.value = false
  }
})

const emit = defineEmits<{
  delete: []
  edit: []
  copy: []
  run: []
  export: []
  setGroup: [groupId: string]
}>()

function handleCopy() {
  isCopying.value = true
  emit('copy')
  setTimeout(() => { isCopying.value = false }, 4000)
}

function handleDelete() {
  isDeleting.value = true
  emit('delete')
  confirmingDelete.value = false
}

const showWeightTooltip = ref(false)
const tooltipX = ref(0)
const tooltipY = ref(0)
const tooltipArrowOffset = ref(0)
const tooltipRef = ref<HTMLElement | null>(null)

async function onWeightHover(e: MouseEvent) {
  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
  const anchorCenterX = rect.left + rect.width / 2
  tooltipX.value = anchorCenterX
  tooltipY.value = rect.top
  tooltipArrowOffset.value = 0
  showWeightTooltip.value = true

  await nextTick()
  if (!tooltipRef.value) return
  const tipRect = tooltipRef.value.getBoundingClientRect()
  const padding = 8
  const idealLeft = tipRect.left
  const clampedLeft = Math.max(padding, Math.min(window.innerWidth - tipRect.width - padding, idealLeft))
  if (clampedLeft !== idealLeft) {
    tooltipX.value = anchorCenterX + (clampedLeft - idealLeft)
    tooltipArrowOffset.value = anchorCenterX - (clampedLeft + tipRect.width / 2)
  }
}

const subjectLabel = computed(() => {
  const map: Record<string, string> = { ships: 'Ships', artifacts: 'Artifacts', missions: 'Ships' }
  return map[props.def.subject] ?? props.def.subject
})

const modeLabel = computed(() => {
  return props.def.mode === 'time_series' ? 'Time series' : 'Aggregate'
})

const dimensionLabelMap: Record<string, string> = {
  ship_type: 'Ship',
  duration_type: 'Duration',
  level: 'Level',
  mission_type: 'Type',
  mission_target: 'Target',
  artifact_name: 'Artifact',
  rarity: 'Rarity',
  tier: 'Tier',
  spec_type: 'Spec',
  time_bucket: '',
}

const groupByLabel = computed(() => {
  const g = props.def.groupBy
  if (!g || g === 'time_bucket') return ''
  return dimensionLabelMap[g] ?? g
})

const secondaryGroupByLabel = computed(() => {
  const g = props.def.secondaryGroupBy
  if (!g) return ''
  return dimensionLabelMap[g] ?? g
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

function timeSeriesWeightFactors(): WeightFactor[] {
  const factors: WeightFactor[] = []
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
  return factors
}

function aggregateWeightFactors(isArtifacts: boolean): WeightFactor[] {
  const factors: WeightFactor[] = []
  if (isArtifacts) {
    factors.push({ label: 'Subject: Artifact drops (drop table scan)', colorClass: 'text-yellow-400' })
    if (checkHasArtifactScopeFilter()) {
      factors.push({ label: 'Artifact filter: Active (limits scan)', colorClass: 'text-green-400' })
    }
  } else if (checkHasArtifactScopeFilter()) {
    factors.push({ label: 'Artifact filter: Active (cross-table join)', colorClass: 'text-yellow-400' })
  }
  return factors
}

const weightFactors = computed((): WeightFactor[] => {
  const isTimeSeries = props.def.mode === 'time_series'
  const modeLabel = isTimeSeries ? 'Mode: Time series (row scan)' : 'Mode: Aggregate (indexed)'
  const modeColor = isTimeSeries ? 'text-yellow-400' : 'text-green-400'
  const extra = isTimeSeries
    ? timeSeriesWeightFactors()
    : aggregateWeightFactors(props.def.subject === 'artifacts')
  return [{ label: modeLabel, colorClass: modeColor }, ...extra]
})

const normalizeLabel = computed(() => {
  switch (props.def.normalizeBy) {
    case 'launches': return 'per launch'
    case 'airtime': return 'per flight hour'
    case 'row_pct': return 'row %'
    case 'col_pct': return 'col %'
    case 'global_pct': return 'global %'
    default: return ''
  }
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
  else if (op === '!=') kept = pairs.filter(p => p.value !== threshold)
  else kept = pairs
  if (isFloat) {
    return {
      labels: kept.map(p => p.label),
      values: [] as number[],
      floatValues: kept.map(p => p.value),
      isFloat: true,
      weight: props.result.weight,
      rowLabels: [] as string[],
      colLabels: [] as string[],
      matrixValues: [] as number[],
      is2D: false,
    }
  }
  return {
    labels: kept.map(p => p.label),
    values: kept.map(p => p.value),
    floatValues: [] as number[],
    isFloat: false,
    weight: props.result.weight,
    rowLabels: [] as string[],
    colLabels: [] as string[],
    matrixValues: [] as number[],
    is2D: false,
  }
})
</script>
