<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center justify-between mb-4">
      <span class="text-xs text-gray-500">Step {{ displayStep }} of {{ totalSteps }}</span>
      <div class="flex gap-1">
        <div
          v-for="n in totalSteps"
          :key="n"
          class="w-4 h-1 rounded-full"
          :class="n <= displayStep ? 'bg-indigo-500' : 'bg-gray-600 border border-gray-500/50'"
        />
      </div>
    </div>

    <!-- Step 1: What do you want to see? -->
    <div v-if="step === 1" class="flex flex-col gap-3">
      <p class="text-xs text-gray-400">What would you like to analyze?</p>
      <div class="grid grid-cols-3 gap-2">
        <button
          v-for="opt in intentOptions"
          :key="opt.value"
          type="button"
          class="flex flex-col items-center gap-1.5 p-3 rounded-lg border text-xs transition-colors focus:outline-none"
          :class="intent === opt.value
            ? 'border-indigo-500 bg-indigo-900/20 text-indigo-300'
            : 'border-gray-700 bg-darker text-gray-400 hover:border-gray-500'"
          @click="selectIntent(opt.value)"
        >
          <span class="text-lg">{{ opt.icon }}</span>
          <span class="font-medium">{{ opt.label }}</span>
          <span class="text-gray-500 text-center leading-tight">{{ opt.hint }}</span>
        </button>
      </div>
    </div>

    <!-- Step 2: Break it down by... -->
    <div v-else-if="step === 2" class="flex flex-col gap-3">
      <p class="text-xs text-gray-400">Break it down by...</p>
      <div class="flex flex-col gap-1.5">
        <button
          v-for="opt in groupByOptions"
          :key="opt.value"
          type="button"
          class="flex items-center gap-2 px-3 py-2 rounded-lg border text-xs text-left transition-colors focus:outline-none"
          :class="groupBy === opt.value
            ? 'border-indigo-500 bg-indigo-900/20 text-indigo-300'
            : 'border-gray-700 bg-darker text-gray-400 hover:border-gray-500'"
          @click="selectGroupBy(opt.value)"
        >
          <span class="flex-1">{{ opt.label }}</span>
        </button>
      </div>
      <button
        type="button"
        class="text-xs text-gray-500 hover:text-gray-400 self-start"
        @click="goBack"
      >Back</button>
    </div>

    <!-- Step 3: Any quick filters? -->
    <div v-else-if="step === 3" class="flex flex-col gap-3">
      <p class="text-xs text-gray-400">Apply a quick filter? (optional)</p>
      <div class="flex flex-col gap-2">
        <label
          v-for="qf in quickFilterOptions"
          :key="qf.key"
          class="flex items-center gap-2 text-xs text-gray-400 cursor-pointer"
        >
          <input
            type="checkbox"
            :value="qf.key"
            v-model="quickFilters"
            class="rounded"
          />
          {{ qf.label }}
        </label>
      </div>
      <div class="flex gap-2">
        <button
          type="button"
          class="text-xs text-gray-500 hover:text-gray-400"
          @click="goBack"
        >← Back</button>
        <button
          type="button"
          class="text-xs text-indigo-400 hover:text-indigo-300"
          @click="step = 4"
        >Next →</button>
      </div>
    </div>

    <!-- Step 4: Name it -->
    <div v-else-if="step === 4" class="flex flex-col gap-3">
      <p class="text-xs text-gray-400">Give it a name</p>
      <input
        v-model="reportName"
        type="text"
        class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
        placeholder="Report name"
        @keydown.enter="handleApply"
      />
      <div class="flex gap-2">
        <button
          type="button"
          class="text-xs text-gray-500 hover:text-gray-400"
          @click="goBack"
        >← Back</button>
        <button
          type="button"
          class="px-3 py-1.5 rounded border border-indigo-600 bg-indigo-700 text-xs text-white hover:bg-indigo-600 disabled:opacity-50"
          :disabled="!reportName.trim()"
          @click="handleApply"
        >Create Report</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ReportFilterCondition, PossibleTarget } from '../types/bridge'

defineProps<{
  possibleTargets: PossibleTarget[]
}>()

const emit = defineEmits<{
  apply: [partial: {
    name: string
    subject: string
    mode: string
    groupBy: string
    displayMode: string
    filters: { and: ReportFilterCondition[], or: never[][] }
  }]
}>()

const step = ref(1)
const intent = ref('')
const groupBy = ref('')
const quickFilters = ref<string[]>([])
const reportName = ref('')

const intentOptions = [
  { value: 'missions', icon: '🚀', label: 'Mission counts', hint: 'Count missions by ship, level, or duration' },
  { value: 'artifacts', icon: '🪨', label: 'Artifact drops', hint: 'Count drops by artifact, rarity, or type' },
  { value: 'time', icon: '📈', label: 'Activity over time', hint: 'Track mission activity across weeks or months' },
]

const groupByOptions = computed(() => {
  if (intent.value === 'artifacts') {
    return [
      { value: 'artifact_name', label: 'Artifact name' },
      { value: 'rarity', label: 'Rarity' },
      { value: 'tier', label: 'Tier' },
      { value: 'spec_type', label: 'Spec type' },
    ]
  }
  return [
    { value: 'ship_type', label: 'Ship type' },
    { value: 'duration_type', label: 'Duration type' },
    { value: 'level', label: 'Level' },
    { value: 'mission_type', label: 'Mission type' },
    { value: 'mission_target', label: 'Mission target' },
  ]
})

const quickFilterOptions = computed(() => {
  const opts: { key: string, label: string, condition: ReportFilterCondition }[] = [
    { key: 'dubcap', label: 'Only dub-cap missions', condition: { topLevel: 'dubcap', op: 'true', val: '' } },
    { key: 'extended', label: 'Only extended missions', condition: { topLevel: 'duration', op: '=', val: '2' } },
  ]
  if (intent.value === 'artifacts') {
    opts.push({ key: 'legendary', label: 'Only legendary artifacts', condition: { topLevel: 'artifact_rarity', op: '=', val: '3' } })
  }
  return opts
})

const totalSteps = computed(() => intent.value === 'time' ? 3 : 4)

const displayStep = computed(() => {
  if (intent.value !== 'time') return step.value
  if (step.value === 1) return 1
  if (step.value === 3) return 2
  return 3
})

function autoName() {
  const intentLabels: Record<string, string> = { missions: 'Mission counts', artifacts: 'Artifact drops', time: 'Activity over time' }
  const groupByLabels: Record<string, string> = {
    ship_type: 'ship type', duration_type: 'duration', level: 'level',
    mission_type: 'mission type', mission_target: 'target',
    artifact_name: 'artifact', rarity: 'rarity', tier: 'tier', spec_type: 'spec type',
    time_bucket: '',
  }
  const base = intentLabels[intent.value] ?? ''
  const by = groupBy.value && groupBy.value !== 'time_bucket' ? ` by ${groupByLabels[groupBy.value] ?? groupBy.value}` : ''
  reportName.value = base + by
}

function selectIntent(value: string) {
  intent.value = value
  if (value === 'time') {
    groupBy.value = 'time_bucket'
    autoName()
    step.value = 3
  } else {
    groupBy.value = ''
    step.value = 2
  }
}

function selectGroupBy(value: string) {
  groupBy.value = value
  autoName()
  step.value = 3
}

function goBack() {
  if (step.value === 4) { step.value = 3; return }
  if (step.value === 3) { step.value = intent.value === 'time' ? 1 : 2; return }
  if (step.value === 2) { step.value = 1; }
}

function handleApply() {
  if (!reportName.value.trim()) return
  const andConditions = quickFilters.value
    .map(key => quickFilterOptions.value.find(q => q.key === key)?.condition)
    .filter((c): c is ReportFilterCondition => c !== undefined)

  const isArtifact = intent.value === 'artifacts'
  let displayMode = 'bar'
  if (intent.value === 'time') displayMode = 'line'
  else if (isArtifact) displayMode = 'pie'
  emit('apply', {
    name: reportName.value.trim(),
    subject: isArtifact ? 'artifacts' : 'missions',
    mode: intent.value === 'time' ? 'time_series' : 'aggregate',
    groupBy: groupBy.value,
    displayMode,
    filters: { and: andConditions, or: [] },
  })
}
</script>
