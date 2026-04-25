<template>
  <div>
    <div v-for="(cond, i) in andConditions" :key="i">
      <div v-if="i !== 0" class="text-gray-400 mt-0_5rem">-- AND --</div>
      <div class="filter-container focus-within:z-10">
        <!-- Field select -->
        <select
          class="filter-select border-gray-300 text-gray-400"
          :value="cond.topLevel"
          @change="onFieldChange(i, ($event.target as HTMLSelectElement).value)"
        >
          <option value="">Field...</option>
          <optgroup v-if="filterFieldOptions.mission.length" label="Mission">
            <option v-for="f in filterFieldOptions.mission" :key="f.value" :value="f.value">
              {{ f.label }}
            </option>
          </optgroup>
          <optgroup v-if="filterFieldOptions.artifact.length" label="Artifact">
            <option v-for="f in filterFieldOptions.artifact" :key="f.value" :value="f.value">
              {{ f.label }}
            </option>
          </optgroup>
        </select>

        <!-- Bool fields: single True/False value select (op encodes the value) -->
        <template v-if="cond.topLevel && isBoolField(cond.topLevel)">
          <select
            class="filter-select border-gray-300 text-gray-400"
            :value="cond.op"
            @change="$emit('update', i, { op: ($event.target as HTMLSelectElement).value })"
          >
            <option value="true">True</option>
            <option value="false">False</option>
          </select>
        </template>

        <!-- Non-bool fields: operator + value -->
        <template v-else-if="cond.topLevel">
          <select
            class="filter-select border-gray-300 text-gray-400"
            :value="cond.op"
            @change="$emit('update', i, { op: ($event.target as HTMLSelectElement).value })"
          >
            <option v-for="op in operatorsForField(cond.topLevel)" :key="op.value" :value="op.value">
              {{ op.label }}
            </option>
          </select>

          <template v-if="cond.op">
            <input
              v-if="isDateField(cond.topLevel)"
              :value="cond.val"
              type="date"
              class="filter-select border-gray-300 text-gray-400"
              @input="$emit('update', i, { val: ($event.target as HTMLInputElement).value })"
            />
            <select
              v-else-if="valueOptionsFor(cond.topLevel).length > 0"
              class="filter-select border-gray-300 text-gray-400"
              :value="cond.val"
              @change="$emit('update', i, { val: ($event.target as HTMLSelectElement).value })"
            >
              <option v-for="opt in valueOptionsFor(cond.topLevel)" :key="opt.value" :value="opt.value">
                {{ opt.text }}
              </option>
            </select>
            <input
              v-else
              :value="cond.val"
              type="text"
              class="filter-select border-gray-300 text-gray-400"
              placeholder="Value"
              @input="$emit('update', i, { val: ($event.target as HTMLInputElement).value })"
            />
          </template>
        </template>

        <!-- Remove button -->
        <button
          type="button"
          title="Remove filter condition"
          class="mr-1rem flex items-center justify-center text-red-700 text-lg max-w-6 max-h-6 min-w-6 min-h-6 border border-red-700 rounded-md bg-transparent mt-0_5rem filter-button"
          @click="$emit('remove', i)"
        >
          &times;
        </button>

        <!-- Incomplete warning -->
        <span v-if="isIncomplete(cond)" class="filter-incomplete">
          (!) Incomplete, will not apply
        </span>
      </div>
    </div>

    <p v-if="andConditions.length === 0" class="text-xs text-gray-500 italic">
      No conditions - report shows all data.
    </p>
  </div>
</template>

<script setup lang="ts">
import type { ReportFilterCondition, PossibleTarget } from '../types/bridge'
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
}>()

const emit = defineEmits<{
  remove: [index: number]
  update: [index: number, patch: Partial<ReportFilterCondition>]
  fieldChange: [index: number, field: string]
}>()

const boolFields = new Set(['dubcap', 'buggedcap'])
const dateFields = new Set(['launchDT', 'returnDT'])

function isBoolField(field: string) { return boolFields.has(field) }
function isDateField(field: string) { return dateFields.has(field) }

function operatorsForField(field: string): { value: string, label: string }[] {
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

function onFieldChange(index: number, field: string) {
  emit('fieldChange', index, field)
}
</script>
