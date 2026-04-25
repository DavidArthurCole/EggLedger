<template>
  <div
    class="popup-selector-main overlay-report"
    tabindex="-1"
    @mousedown.self="requestClose()"
    @keydown.esc="requestClose()"
  >
    <div
      class="bg-dark rounded-lg relative shadow-xl w-full max-w-2xl max-h-9/10-vh flex flex-col transition-colors duration-150"
      :class="closeWarning ? 'ring-2 ring-red-500/60' : ''"
      @mousedown.stop
    >
      <button class="close-button" type="button" @click="requestClose()">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <!-- Header -->
      <div class="flex items-center px-5 py-3 border-b border-gray-700 flex-shrink-0">
        <span class="text-sm font-semibold text-gray-200">
          {{ editingDef ? 'Edit Report' : 'Add Report' }}
        </span>
      </div>

      <!-- Edit form -->
      <div class="flex-1 overflow-y-auto p-5 flex flex-col gap-4">
        <!-- Mode toggle -->
        <div class="flex rounded-md overflow-hidden border border-gray-700 self-start text-xs">
          <button
            type="button"
            class="px-3 py-1.5 transition-colors"
            :class="builderMode === 'basic' ? 'bg-indigo-700 text-white' : 'bg-darker text-gray-400 hover:text-gray-200'"
            @click="builderMode = 'basic'"
          >Basic</button>
          <button
            type="button"
            class="px-3 py-1.5 transition-colors"
            :class="builderMode === 'advanced' ? 'bg-indigo-700 text-white' : 'bg-darker text-gray-400 hover:text-gray-200'"
            @click="builderMode = 'advanced'"
          >Advanced</button>
        </div>

        <!-- Guided mode -->
        <ReportBuilderGuided
          v-if="builderMode === 'basic'"
          :possible-targets="possibleTargets"
          @apply="onGuidedApply"
        />

        <!-- Advanced form fields -->
        <template v-else>
        <!-- Name -->
        <div class="flex flex-col gap-1">
          <label for="rb-name" class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/></svg>
            Name
          </label>
          <input
            id="rb-name"
            v-model="form.name"
            type="text"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            placeholder="Report name"
          />
        </div>

        <!-- Description -->
        <div class="flex flex-col gap-1">
          <label for="rb-description" class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h8"/></svg>
            Description (optional)
          </label>
          <textarea
            id="rb-description"
            v-model="form.description"
            rows="2"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-xs text-gray-400 focus:outline-none focus:border-blue-500 resize-none"
            placeholder="What does this report show?"
          />
        </div>

        <!-- Subject + Mode row -->
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <label for="rb-subject" class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4"/></svg>
              Subject
            </label>
            <select
              id="rb-subject"
              v-model="form.subject"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
              @change="onSubjectChange"
            >
              <option value="ships">Ships</option>
              <option value="artifacts">Artifacts</option>
              <option value="missions">Missions</option>
            </select>
          </div>
          <div class="flex flex-col gap-1">
            <label for="rb-mode" class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg>
              Mode
            </label>
            <select
              id="rb-mode"
              v-model="form.mode"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
              @change="onSubjectOrModeChange"
            >
              <option value="aggregate">Aggregate</option>
              <option value="time_series">Time Series</option>
            </select>
          </div>
        </div>

        <!-- Display mode + Group by row -->
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <label for="rb-display-mode" class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/></svg>
              Display
            </label>
            <select
              id="rb-display-mode"
              v-model="form.displayMode"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            >
              <option value="bar">Bar chart</option>
              <option value="line">Line chart</option>
              <option value="pie">Pie chart</option>
              <option value="grid">Table</option>
            </select>
          </div>
          <div class="flex flex-col gap-1">
            <label for="rb-group-by" class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A2 2 0 013 12V7a4 4 0 014-4z"/></svg>
              Group by
            </label>
            <select
              id="rb-group-by"
              v-model="form.groupBy"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            >
              <option v-for="opt in groupByOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </div>
        </div>

        <!-- Color picker (bar/line/pie only) -->
        <div v-if="form.displayMode !== 'grid'" class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01"/></svg>
            {{ form.displayMode === 'pie' ? 'Fallback color' : 'Chart color' }}
          </span>
          <ColorPicker v-model="form.color" />
        </div>

        <!-- Per-slice colors (pie only) -->
        <div v-if="form.displayMode === 'pie'" class="flex flex-col gap-2">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01"/></svg>
            Slice colors
          </span>
          <div v-if="pieLabels.length > 0" class="grid grid-cols-2 gap-1.5">
            <div
              v-for="label in pieLabels"
              :key="label"
              class="flex items-center gap-2 min-w-0"
            >
              <ColorPicker
                :model-value="getLabelColor(label)"
                @update:model-value="setLabelColor(label, $event)"
              />
              <span class="text-xs text-gray-400 truncate">{{ label }}</span>
            </div>
          </div>
          <p v-else class="text-xs text-gray-600 italic">Run the report once to set per-slice colors.</p>
        </div>

        <!-- Time bucket (time_series only) -->
        <div v-if="form.mode === 'time_series'" class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <label for="rb-time-bucket" class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
              Time bucket
            </label>
            <select
              id="rb-time-bucket"
              v-model="form.timeBucket"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            >
              <option value="day">Day</option>
              <option value="week">Week</option>
              <option value="month">Month</option>
              <option value="year">Year</option>
              <option value="custom">Custom window</option>
            </select>
          </div>
          <div v-if="form.timeBucket === 'custom'" class="flex flex-col gap-1">
            <span class="text-xs text-gray-400">Last N</span>
            <div class="flex gap-2">
              <input
                id="rb-custom-bucket-n"
                v-model.number="form.customBucketN"
                type="number"
                min="1"
                class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500 w-16"
                placeholder="N"
              />
              <select
                id="rb-custom-bucket-unit"
                v-model="form.customBucketUnit"
                class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500 flex-1"
              >
                <option value="day">Days</option>
                <option value="week">Weeks</option>
                <option value="month">Months</option>
              </select>
            </div>
          </div>
        </div>

        <!-- Grid size + live preview side by side -->
        <div class="flex flex-row gap-4 items-start">
          <!-- Grid size picker -->
          <div class="flex flex-col gap-2">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 5a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1H5a1 1 0 01-1-1V5zM14 5a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1V5zM4 15a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1H5a1 1 0 01-1-1v-4zM14 15a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1v-4z"/></svg>
              Grid size
              <span class="text-gray-500 ml-1">{{ form.gridW }}x{{ form.gridH }}</span>
            </span>
            <!-- 4x4 visual picker -->
            <div class="flex flex-col gap-0.5 self-start" @mouseleave="clearHover()">
              <div v-for="r in 4" :key="r" class="flex gap-0.5">
                <div
                  v-for="c in 4"
                  :key="c"
                  class="w-5 h-5 rounded-sm border cursor-pointer transition-colors"
                  :class="cellClass(c, r)"
                  @mouseenter="setHover(c, r)"
                  @click="selectCell(c, r)"
                />
              </div>
            </div>
          </div>

          <!-- Live preview -->
          <div class="flex flex-col gap-1">
            <span class="text-xs text-gray-400">Preview</span>
            <div
              class="bg-darker rounded-lg border border-gray-700 p-3 overflow-hidden flex flex-col"
              :style="{ width: (form.gridW * 110) + 'px', height: (form.gridH * 110) + 'px' }"
            >
              <div class="text-xs font-medium text-gray-300 mb-2 truncate flex-shrink-0">{{ form.name || 'Untitled report' }}</div>
              <div class="flex-1 min-h-0">
                <template v-if="form.displayMode === 'bar'">
                  <div class="flex flex-col justify-center gap-1 h-full">
                    <div v-for="w in [80, 55, 40, 25]" :key="w" class="flex items-center gap-1">
                      <div class="w-8 h-1.5 bg-gray-700 rounded-sm flex-shrink-0" />
                      <div class="h-1.5 rounded-sm" :style="{ width: w + '%', backgroundColor: form.color + '88' }" />
                    </div>
                  </div>
                </template>
                <template v-else-if="form.displayMode === 'line'">
                  <div class="flex items-end gap-0.5 h-full">
                    <div v-for="(h, i) in [30, 55, 45, 70, 50, 80, 60]" :key="i" class="flex-1 rounded-sm" :style="{ height: h + '%', backgroundColor: form.color + '88' }" />
                  </div>
                </template>
                <template v-else-if="form.displayMode === 'pie'">
                  <svg viewBox="0 0 100 100" class="w-full h-full">
                    <path d="M 50 50 L 50 10 A 40 40 0 0 1 73.5 82.4 Z" :fill="form.color + 'ee'" />
                    <path d="M 50 50 L 73.5 82.4 A 40 40 0 0 1 10 50 Z" :fill="form.color + '99'" />
                    <path d="M 50 50 L 10 50 A 40 40 0 0 1 50 10 Z" :fill="form.color + '55'" />
                  </svg>
                </template>
                <template v-else>
                  <div class="flex flex-col justify-center gap-0.5 h-full">
                    <div v-for="i in 4" :key="i" class="flex gap-2 py-0.5 border-b border-gray-700/50 last:border-0">
                      <div class="flex-1 h-1.5 bg-gray-700 rounded-sm" />
                      <div class="w-8 h-1.5 bg-gray-600 rounded-sm" />
                    </div>
                  </div>
                </template>
              </div>
            </div>
            <p class="text-xs text-gray-500 text-center">{{ form.gridW }}x{{ form.gridH }} - {{ modeLabel }} - {{ subjectLabel }}</p>
          </div>
        </div>

        <!-- Filters -->
        <div class="flex flex-col gap-2">
          <div class="flex items-center justify-between">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"/></svg>
              Filters (AND)
            </span>
            <button
              type="button"
              class="text-xs text-indigo-400 hover:text-indigo-300"
              @click="addAndCondition"
            >
              + Add condition
            </button>
          </div>

          <AndOnlyFilter
            :and-conditions="andConditions"
            :filter-field-options="filterFieldOptions"
            :possible-targets="possibleTargets"
            @remove="removeAndCondition"
            @update="updateAndCondition"
            @field-change="onFieldChange"
          />
        </div>

        <!-- Value filter -->
        <div class="flex flex-col gap-2">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg>
            Value filter (optional)
          </span>
          <div class="flex items-center gap-2">
            <span class="text-xs text-gray-500">Only show results where count</span>
            <select
              v-model="form.valueFilterOp"
              class="bg-darker border border-gray-700 rounded px-2 py-1 text-xs text-gray-300 focus:outline-none focus:border-blue-500"
            >
              <option value="">None</option>
              <option value=">">&gt; (greater than)</option>
              <option value=">=">&gt;= (at least)</option>
              <option value="<">&lt; (less than)</option>
              <option value="<=">&lt;= (at most)</option>
              <option value="=">=  (equal to)</option>
            </select>
            <input
              v-if="form.valueFilterOp"
              v-model.number="form.valueFilterThreshold"
              type="number"
              min="0"
              class="bg-darker border border-gray-700 rounded px-2 py-1 text-xs text-gray-300 focus:outline-none focus:border-blue-500 w-20"
              placeholder="0"
            />
          </div>
        </div>

        <!-- Normalize by (artifacts + aggregate only) -->
        <div v-if="form.subject === 'artifacts' && form.mode === 'aggregate'" class="flex flex-col gap-2">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 11h.01M12 11h.01M15 11h.01M4 19h16a2 2 0 002-2V7a2 2 0 00-2-2H4a2 2 0 00-2 2v10a2 2 0 002 2z"/></svg>
            Normalize by (optional)
          </span>
          <select
            v-model="form.normalizeBy"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
          >
            <option value="none">None (raw count)</option>
            <option value="launches">Per launch</option>
            <option value="airtime">Per flight hour</option>
          </select>
        </div>
        </template>
      </div>

      <!-- Footer -->
      <div class="flex flex-col gap-1.5 px-5 py-3 border-t border-gray-700 flex-shrink-0">
        <p v-if="closeWarning" class="text-xs text-red-400 text-center">
          Unsaved changes - click Cancel again to discard, or Save to keep them.
        </p>
        <div class="flex gap-2">
        <button
          type="button"
          class="flex-1 px-3 py-1.5 rounded border text-xs focus:outline-none"
          :class="closeWarning
            ? 'border-red-600 text-red-400 hover:bg-red-900/20'
            : 'border-gray-600 text-gray-400 hover:text-gray-200 hover:border-gray-400'"
          @click="requestClose()"
        >
          Cancel
        </button>
        <button
          type="button"
          class="flex-1 px-3 py-1.5 rounded border border-indigo-600 bg-indigo-700 text-xs text-white hover:bg-indigo-600 disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none"
          :disabled="!canSave"
          @click="handleSave"
        >
          Save
        </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed, watch, ref, onMounted, nextTick } from 'vue'
import type { ReportDefinition, ReportFilterCondition, PossibleTarget } from '../types/bridge'
import { useReportFilters } from '../composables/useReportFilters'
import AndOnlyFilter from './AndOnlyFilter.vue'
import ReportBuilderGuided from './ReportBuilderGuided.vue'
import ColorPicker from './ColorPicker.vue'
import {
  getMissionFilterValueOptions,
  getTargetFilterOptions,
  getArtifactFilterValueOptions,
} from '../utils/filterOptions'

const props = defineProps<{
  accountId: string
  editingDef: ReportDefinition | null
  /** Labels from the last executed result, used to populate per-slice color pickers */
  editingResultLabels?: string[]
}>()

const emit = defineEmits<{
  saved: [def: ReportDefinition]
  close: []
}>()

const { andConditions, addAndCondition, removeAndCondition, updateAndCondition, toReportFilters, fromReportFilters, clearFilters } = useReportFilters()

const possibleTargets = ref<PossibleTarget[]>([])
const isDirty = ref(false)
const closeWarning = ref(false)
const builderMode = ref<'basic' | 'advanced'>('basic')
const hoverW = ref(0)
const hoverH = ref(0)
const labelColorsMap = ref<Record<string, string>>({})

function cellClass(c: number, r: number): string {
  const confirmed = c <= form.gridW && r <= form.gridH
  if (hoverW.value > 0) {
    const hovered = c <= hoverW.value && r <= hoverH.value
    if (confirmed && hovered) return 'bg-indigo-600 border-indigo-500'
    if (hovered) return 'bg-indigo-600/75 border-indigo-500/50'
    if (confirmed) return 'bg-indigo-800/60 border-indigo-700/50'
    return 'bg-gray-800 border-gray-600 hover:border-gray-400'
  }
  return confirmed ? 'bg-indigo-600 border-indigo-500' : 'bg-gray-800 border-gray-600 hover:border-gray-400'
}

function clearHover() {
  hoverW.value = 0
  hoverH.value = 0
}

function setHover(c: number, r: number) {
  hoverW.value = c
  hoverH.value = r
}

function selectCell(c: number, r: number) {
  form.gridW = c
  form.gridH = r
}

function parseLabelColors(raw: string): Record<string, string> {
  if (!raw) return {}
  try {
    return JSON.parse(raw) as Record<string, string>
  } catch {
    return {}
  }
}

function makeBlankForm() {
  return {
    name: '',
    description: '',
    subject: 'ships',
    mode: 'aggregate',
    displayMode: 'bar',
    groupBy: 'ship_type',
    timeBucket: 'month',
    customBucketN: 3,
    customBucketUnit: 'month',
    gridW: 2,
    gridH: 2,
    color: '#6366f1',
    valueFilterOp: '',
    valueFilterThreshold: 0,
    normalizeBy: 'none',
  }
}

const form = reactive(makeBlankForm())

watch(
  () => props.editingDef,
  (def) => {
    builderMode.value = def ? 'advanced' : 'basic'
    isDirty.value = false
    closeWarning.value = false
    if (def) {
      form.name = def.name
      form.description = def.description || ''
      form.subject = def.subject
      form.mode = def.mode
      form.displayMode = def.displayMode
      form.groupBy = def.groupBy
      form.timeBucket = def.timeBucket || 'month'
      form.customBucketN = def.customBucketN || 3
      form.customBucketUnit = def.customBucketUnit || 'month'
      form.gridW = def.gridW || 2
      form.gridH = def.gridH || 2
      form.color = def.color || '#6366f1'
      form.valueFilterOp = def.valueFilterOp || ''
      form.valueFilterThreshold = def.valueFilterThreshold || 0
      form.normalizeBy = def.normalizeBy || 'none'
      labelColorsMap.value = parseLabelColors(def.labelColors)
      fromReportFilters(def.filters)
    } else {
      Object.assign(form, makeBlankForm())
      labelColorsMap.value = {}
      clearFilters()
    }
    nextTick(() => { isDirty.value = false })
  },
  { immediate: true },
)

watch(form, () => { isDirty.value = true }, { deep: true })
watch(andConditions, () => { isDirty.value = true }, { deep: true })
watch(labelColorsMap, () => { isDirty.value = true }, { deep: true })

const pieLabels = computed(() => {
  if (form.displayMode !== 'pie') return []
  const resultLabels = props.editingResultLabels ?? []
  if (resultLabels.length > 0) return resultLabels
  return Object.keys(labelColorsMap.value)
})

function hexToHsl(hex: string): [number, number, number] {
  const rv = Number.parseInt(hex.slice(1, 3), 16) / 255
  const gv = Number.parseInt(hex.slice(3, 5), 16) / 255
  const bv = Number.parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(rv, gv, bv)
  const min = Math.min(rv, gv, bv)
  const l = (max + min) / 2
  const d = max - min
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1))
  let h = 0
  if (d !== 0) {
    switch (max) {
      case rv: h = ((gv - bv) / d + 6) % 6; break
      case gv: h = (bv - rv) / d + 2; break
      default: h = (rv - gv) / d + 4
    }
    h *= 60
  }
  return [h, s, l]
}

function hslToHex(h: number, s: number, l: number): string {
  const a = s * Math.min(l, 1 - l)
  const f = (n: number) => {
    const k = (n + h / 30) % 12
    const color = l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1)
    return Math.round(255 * color).toString(16).padStart(2, '0')
  }
  return `#${f(0)}${f(8)}${f(4)}`
}

function autoSliceColors(baseColor: string, count: number): string[] {
  const [h, s, l] = hexToHsl(baseColor)
  return Array.from({ length: count }, (_, i) =>
    hslToHex(((h + (i * 360) / count) % 360), s, l),
  )
}

function setLabelColor(label: string, color: string) {
  labelColorsMap.value = { ...labelColorsMap.value, [label]: color }
}

function getLabelColor(label: string): string {
  if (labelColorsMap.value[label]) return labelColorsMap.value[label]
  const idx = pieLabels.value.indexOf(label)
  const colors = autoSliceColors(form.color, pieLabels.value.length)
  return idx >= 0 ? (colors[idx] ?? form.color) : form.color
}

const subjectLabel = computed(() => {
  const map: Record<string, string> = { ships: 'Ships', artifacts: 'Artifacts', missions: 'Missions' }
  return map[form.subject] ?? form.subject
})

const modeLabel = computed(() =>
  form.mode === 'time_series' ? 'Time series' : 'Aggregate',
)

const shipMissionAggregateOptions = [
  { value: 'ship_type', label: 'Ship Type' },
  { value: 'duration_type', label: 'Duration Type' },
  { value: 'level', label: 'Level' },
  { value: 'mission_type', label: 'Mission Type' },
  { value: 'mission_target', label: 'Mission Target' },
]

const artifactAggregateOptions = [
  { value: 'artifact_name', label: 'Artifact Name' },
  { value: 'rarity', label: 'Rarity' },
  { value: 'tier', label: 'Tier' },
  { value: 'spec_type', label: 'Spec Type' },
]

const timeSeriesOption = [{ value: 'time_bucket', label: 'Time Bucket' }]

const groupByOptions = computed(() => {
  if (form.mode === 'time_series') return timeSeriesOption
  if (form.subject === 'artifacts') return artifactAggregateOptions
  return shipMissionAggregateOptions
})

const missionFilterFields = [
  { value: 'ship', label: 'Ship' },
  { value: 'duration', label: 'Duration' },
  { value: 'level', label: 'Level' },
  { value: 'target', label: 'Target' },
  { value: 'type', label: 'Mission Type' },
  { value: 'launchDT', label: 'Launch Date' },
  { value: 'returnDT', label: 'Return Date' },
  { value: 'dubcap', label: 'Dub cap' },
  { value: 'buggedcap', label: 'Bugged cap' },
]

const artifactFilterFields = [
  { value: 'artifact_name', label: 'Name' },
  { value: 'artifact_rarity', label: 'Rarity' },
  { value: 'artifact_tier', label: 'Tier' },
  { value: 'artifact_spec_type', label: 'Spec Type' },
  { value: 'artifact_quality', label: 'Quality' },
]

const filterFieldOptions = computed(() => ({
  mission: form.subject === 'artifacts' ? [] : missionFilterFields,
  artifact: form.subject === 'artifacts' ? artifactFilterFields : [],
}))

const boolFields = new Set(['dubcap', 'buggedcap'])
const dateFields = new Set(['launchDT', 'returnDT'])

function isBoolField(field: string) { return boolFields.has(field) }
function isDateField(field: string) { return dateFields.has(field) }

function valueOptionsForField(topLevel: string) {
  if (topLevel === 'target') return getTargetFilterOptions(possibleTargets.value)
  const missionOpts = getMissionFilterValueOptions(topLevel)
  if (missionOpts.length > 0) return missionOpts
  return getArtifactFilterValueOptions(topLevel)
}

function onFieldChange(index: number, field: string) {
  const defaultOp = isBoolField(field) ? 'true' : '='
  const opts = valueOptionsForField(field)
  const defaultVal = opts.length > 0 ? String(opts[0].value) : ''
  updateAndCondition(index, { topLevel: field, op: defaultOp, val: defaultVal })
}

function onSubjectOrModeChange() {
  const opts = groupByOptions.value
  if (!opts.some(o => o.value === form.groupBy) && opts.length > 0) {
    form.groupBy = opts[0].value
  }
}

function onSubjectChange() {
  clearFilters()
  onSubjectOrModeChange()
}

const canSave = computed(() => {
  if (!form.name.trim()) return false
  return !andConditions.value.some(c => {
    if (c.topLevel === '') return false
    if (c.op === '') return true
    if (isBoolField(c.topLevel)) return false
    return c.val === ''
  })
})

function requestClose() {
  if (!isDirty.value) {
    emit('close')
    return
  }
  if (closeWarning.value) {
    emit('close')
    return
  }
  closeWarning.value = true
}

function clamp(v: number, min: number, max: number) {
  return Math.min(Math.max(v, min), max)
}

function handleSave() {
  isDirty.value = false
  closeWarning.value = false
  const def: ReportDefinition = {
    id: props.editingDef?.id ?? '',
    accountId: props.accountId,
    name: form.name.trim(),
    description: form.description,
    subject: form.subject,
    mode: form.mode,
    displayMode: form.displayMode,
    groupBy: form.groupBy,
    timeBucket: form.timeBucket,
    customBucketN: form.customBucketN,
    customBucketUnit: form.customBucketUnit,
    filters: toReportFilters(),
    gridX: 0,
    gridY: 0,
    gridW: clamp(form.gridW, 1, 4),
    gridH: clamp(form.gridH, 1, 4),
    color: form.color,
    weight: '',
    sortOrder: 0,
    createdAt: 0,
    updatedAt: 0,
    valueFilterOp: form.valueFilterOp,
    valueFilterThreshold: form.valueFilterThreshold,
    normalizeBy: form.subject === 'artifacts' && form.mode === 'aggregate' ? form.normalizeBy : 'none',
    chartType: '',
    groupId: props.editingDef?.groupId ?? '',
    labelColors: form.displayMode === 'pie' && Object.keys(labelColorsMap.value).length > 0
      ? JSON.stringify(labelColorsMap.value)
      : '',
  }
  emit('saved', def)
}

function onGuidedApply(partial: {
  name: string
  subject: string
  mode: string
  groupBy: string
  displayMode: string
  filters: { and: ReportFilterCondition[], or: never[][] }
}) {
  form.name = partial.name
  form.subject = partial.subject
  form.mode = partial.mode
  form.groupBy = partial.groupBy
  form.displayMode = partial.displayMode
  fromReportFilters(partial.filters)
  builderMode.value = 'advanced'
}

onMounted(async () => {
  possibleTargets.value = await globalThis.getPossibleTargets()
  nextTick(() => {
    const el = document.querySelector('.overlay-report') as HTMLElement | null
    el?.focus()
  })
})
</script>
