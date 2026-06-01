<template>
  <div>
    <div v-for="(cond, i) in andConditions" :key="i">
      <div v-if="i !== 0" class="text-xs text-gray-600 italic my-1">and</div>
      <div class="flex flex-col gap-1.5">
        <div class="flex flex-wrap items-center gap-2.5">
          <select
            class="rb-control min-w-28"
            :value="cond.topLevel"
            @change="onFieldChange(i, ($event.target as HTMLSelectElement).value)"
          >
            <option value="" disabled>{{ fieldPlaceholder }}</option>
            <option v-for="f in allFieldOptions" :key="f.value" :value="f.value">{{ f.label }}</option>
          </select>

          <template v-if="cond.topLevel && isBoolField(cond.topLevel)">
            <select
              class="rb-control w-14 flex-shrink-0 opacity-60 cursor-not-allowed"
              disabled
            >
              <option value="">is</option>
            </select>
            <select
              class="rb-control min-w-0"
              :value="cond.op"
              @change="$emit('update', i, { op: ($event.target as HTMLSelectElement).value })"
            >
              <option value="true">True</option>
              <option value="false">False</option>
            </select>
          </template>

          <template v-else-if="cond.topLevel">
            <select
              class="rb-control min-w-0"
              :value="cond.op"
              @change="$emit('update', i, { op: ($event.target as HTMLSelectElement).value })"
            >
              <option v-for="op in operatorsForField(cond.topLevel)" :key="op.value" :value="op.value">{{ op.label }}</option>
            </select>

            <template v-if="cond.op">
              <button
                v-if="cond.topLevel === 'drops'"
                type="button"
                class="rb-control hover:border-blue-500 min-w-28 text-left"
                :class="dropColorClass(cond.val)"
                @click="$emit('openDropPicker', i, $event)"
              >
                {{ dropLabel(cond.val) }}
              </button>
              <input
                v-else-if="isDateField(cond.topLevel)"
                :value="cond.val"
                type="date"
                class="rb-control min-w-0 w-32"
                @input="$emit('update', i, { val: ($event.target as HTMLInputElement).value })"
              />
              <select
                v-else-if="valueOptionsFor(cond.topLevel).length > 0"
                class="rb-control min-w-28"
                :value="cond.val"
                @change="$emit('update', i, { val: ($event.target as HTMLSelectElement).value })"
              >
                <option v-for="opt in valueOptionsFor(cond.topLevel)" :key="opt.value" :value="opt.value">{{ opt.text }}</option>
              </select>
              <input
                v-else
                :value="cond.val"
                type="text"
                class="rb-control min-w-0 w-32"
                placeholder="Value"
                @input="$emit('update', i, { val: ($event.target as HTMLInputElement).value })"
              />
            </template>
          </template>

          <button
            type="button"
            title="Remove filter condition"
            class="text-red-600 hover:text-red-400 px-1 text-sm leading-none flex-shrink-0"
            @click="$emit('remove', i)"
          >
            &times;
          </button>
        </div>
        <span v-if="isIncomplete(cond)" class="text-xs text-yellow-500">Incomplete - will not apply</span>
      </div>
    </div>

    <p v-if="andConditions.length === 0" class="text-xs text-gray-500 italic">
      No conditions - report shows all data.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ReportFilterCondition, PossibleTarget } from '../types/bridge'
import type { FilterOption } from '../utils/filterOptions'
import {
  getMissionFilterValueOptions,
  getTargetFilterOptions,
  getArtifactFilterValueOptions,
} from '../utils/filterOptions'

interface FieldOption {
  value: string
  label: string
}

const props = defineProps<{
  andConditions: ReportFilterCondition[]
  filterFieldOptions: { mission: FieldOption[], artifact: FieldOption[] }
  possibleTargets: PossibleTarget[]
  dropOptions: FilterOption[]
}>()

const emit = defineEmits<{
  remove: [index: number]
  update: [index: number, patch: Partial<ReportFilterCondition>]
  fieldChange: [index: number, field: string]
  openDropPicker: [index: number, ev: MouseEvent]
}>()

const boolFields = new Set(['dubcap', 'buggedcap'])
const dateFields = new Set(['launchDT', 'returnDT'])

function isBoolField(field: string) { return boolFields.has(field) }
function isDateField(field: string) { return dateFields.has(field) }

const allFieldOptions = computed(() => [
  ...props.filterFieldOptions.mission,
  ...props.filterFieldOptions.artifact,
])

const fieldPlaceholder = computed(() => {
  const hasMission = props.filterFieldOptions.mission.length > 0
  const hasArtifact = props.filterFieldOptions.artifact.length > 0
  if (hasArtifact && !hasMission) return 'Artifact field...'
  if (hasMission && !hasArtifact) return 'Mission field...'
  return 'Field...'
})

function operatorsForField(field: string): { value: string, label: string }[] {
  if (field === 'drops') {
    return [
      { value: 'c', label: 'contains' },
      { value: 'dnc', label: 'does not contain' },
    ]
  }
  if (dateFields.has(field)) {
    return [
      { value: '=', label: 'on' },
      { value: '<', label: 'before' },
      { value: '>', label: 'after' },
      { value: '<=', label: 'on or before' },
      { value: '>=', label: 'on or after' },
    ]
  }
  if (field === 'target' || field === 'type' || field === 'farm') {
    return [
      { value: '=', label: 'is' },
      { value: '!=', label: 'is not' },
    ]
  }
  if (field === 'artifact_name' || field === 'artifact_spec_type') {
    return [
      { value: '=', label: 'is' },
      { value: '!=', label: 'is not' },
    ]
  }
  return [
    { value: '=', label: 'is' },
    { value: '!=', label: 'is not' },
    { value: '>', label: 'greater than' },
    { value: '<', label: 'less than' },
    { value: '>=', label: 'at least' },
    { value: '<=', label: 'at most' },
  ]
}

function valueOptionsFor(topLevel: string) {
  if (topLevel === 'target') return getTargetFilterOptions(props.possibleTargets)
  const missionOpts = getMissionFilterValueOptions(topLevel)
  if (missionOpts.length > 0) return missionOpts
  return getArtifactFilterValueOptions(topLevel)
}

function isIncomplete(cond: ReportFilterCondition): boolean {
  if (!cond.topLevel) return false
  if (isBoolField(cond.topLevel)) return !cond.op
  if (!cond.op) return true
  if (isDateField(cond.topLevel)) return !cond.val
  if (valueOptionsFor(cond.topLevel).length === 0 && !cond.val) return true
  return false
}

function dropLabel(val: string): string {
  if (!val) return 'Select drop...'
  const found = props.dropOptions.find(o => o.value === val)
  return found ? found.text : 'Select drop...'
}

// Rarity color for the selected drop, parsed from the composite value
// (name_level_rarity_quality), matching the main filter's coloring.
function dropColorClass(val: string): string {
  switch (val.split('_')[2]) {
    case '1': return 'text-rarity-1'
    case '2': return 'text-rarity-2'
    case '3': return 'text-rarity-3'
    default: return ''
  }
}

function onFieldChange(index: number, field: string) {
  emit('fieldChange', index, field)
}
</script>
