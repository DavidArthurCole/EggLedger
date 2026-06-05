<template>
  <div>
    <div v-for="(cond, i) in andConditions" :key="i">
      <div v-if="i !== 0" class="text-xs text-gray-600 italic my-1">and</div>
      <div class="flex flex-col gap-1.5">
        <div class="flex flex-wrap items-center gap-2.5">
          <select
            class="rb-control flex-1 min-w-36"
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
                v-if="valueKindFor(cond.topLevel) === 'modal'"
                type="button"
                class="rb-control hover:border-blue-500 min-w-28 text-left"
                :class="modalColorClass(cond.topLevel, cond.val)"
                @click="$emit('openPicker', cond.topLevel, i, $event)"
              >
                {{ modalLabel(cond.topLevel, cond.val) }}
              </button>
              <input
                v-else-if="valueKindFor(cond.topLevel) === 'date'"
                :value="cond.val"
                type="date"
                class="rb-control min-w-0 w-32"
                @input="$emit('update', i, { val: ($event.target as HTMLInputElement).value })"
              />
              <input
                v-else-if="valueKindFor(cond.topLevel) === 'number'"
                :value="cond.val"
                type="number"
                step="any"
                class="rb-control min-w-0 w-32"
                placeholder="Value"
                @input="$emit('update', i, { val: ($event.target as HTMLInputElement).value })"
              />
              <select
                v-else-if="valueKindFor(cond.topLevel) === 'select'"
                class="rb-control min-w-28"
                :value="cond.val"
                @change="$emit('update', i, { val: ($event.target as HTMLSelectElement).value })"
              >
                <option v-for="opt in valueOptionsFor(cond.topLevel)" :key="opt.value" :value="opt.value">{{ opt.text }}</option>
              </select>
              <select
                v-else
                class="rb-control min-w-28 opacity-60 cursor-not-allowed"
                disabled
              >
                <option value="">unsupported</option>
              </select>
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
import type { ReportFilterCondition, PossibleTarget, PossibleArtifact } from '../types/bridge'
import { getReportField } from '../utils/filterFields'
import type { FilterFieldCtx } from '../utils/filterFields'

interface FieldOption {
  value: string
  label: string
}

const props = defineProps<{
  andConditions: ReportFilterCondition[]
  filterFieldOptions: { mission: FieldOption[], artifact: FieldOption[] }
  possibleTargets: PossibleTarget[]
  artifactConfigs: PossibleArtifact[]
  maxQuality: number
}>()

const emit = defineEmits<{
  remove: [index: number]
  update: [index: number, patch: Partial<ReportFilterCondition>]
  fieldChange: [index: number, field: string]
  openPicker: [field: string, index: number, ev: MouseEvent]
}>()

const fieldCtx = computed<FilterFieldCtx>(() => ({
  possibleTargets: props.possibleTargets,
  artifactConfigs: props.artifactConfigs,
  maxQuality: props.maxQuality,
}))

function isBoolField(field: string) { return getReportField(field)?.valueKind === 'bool' }

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
  return getReportField(field)?.ops ?? []
}

function valueKindFor(field: string): string {
  return getReportField(field)?.valueKind ?? ''
}

function valueOptionsFor(topLevel: string) {
  return getReportField(topLevel)?.optionsSource?.(fieldCtx.value) ?? []
}

function isIncomplete(cond: ReportFilterCondition): boolean {
  if (!cond.topLevel) return false
  if (isBoolField(cond.topLevel)) return !cond.op
  if (!cond.op) return true
  return !cond.val
}

function modalLabel(field: string, val: string): string {
  const placeholder = field === 'drops' ? 'Select drop...' : 'Select...'
  if (!val) return placeholder
  const found = valueOptionsFor(field).find(o => o.value === val)
  return found ? found.text : placeholder
}

/**
 * Rarity color for a selected drop value, parsed from the composite value
 * (name_level_rarity_quality), matching the main filter's coloring. Only the drops field carries
 * rarity in its value, so other modal fields get no color override.
 */
function modalColorClass(field: string, val: string): string {
  if (field !== 'drops') return ''
  switch (val.split('_')[2]) {
    case '1': return '!text-rarity-1'
    case '2': return '!text-rarity-2'
    case '3': return '!text-rarity-3'
    default: return ''
  }
}

function onFieldChange(index: number, field: string) {
  emit('fieldChange', index, field)
}
</script>
