<template>
  <div
    class="popup-selector-main overlay-report"
    tabindex="-1"
    @mousedown.self="requestClose()"
    @keydown.esc="requestClose()"
  >
    <div
      class="bg-dark rounded-lg relative shadow-xl w-full max-w-2xl max-h-90vh flex flex-col transition-colors duration-150"
      :class="closeWarning ? 'ring-2 ring-red-500/60' : ''"
      @mousedown.stop
    >
      <button class="close-button" type="button" @click="requestClose()">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <!-- Header -->
      <div class="flex items-center px-5 py-3 border-b border-gray-700 flex-shrink-0 gap-3">
        <span class="text-sm font-semibold text-gray-200">
          {{ editingDef ? 'Edit Report' : 'Add Report' }}
        </span>
        <div v-if="!editingDef && builderMode === 'basic'" class="flex rounded-md overflow-hidden border border-gray-700 flex-shrink-0 text-xs">
          <button type="button" class="px-3 py-1.5 transition-colors bg-indigo-700 text-white">Basic</button>
          <button type="button" class="px-3 py-1.5 transition-colors bg-darker text-gray-400 hover:text-gray-200"
            @click="builderMode = 'advanced'">Advanced</button>
        </div>
      </div>

      <!-- Edit form -->
      <div class="flex-1 overflow-y-auto p-5 flex flex-col gap-4">
        <!-- Guided mode -->
        <ReportBuilderGuided
          v-if="builderMode === 'basic'"
          :possible-targets="possibleTargets"
          @apply="onGuidedApply"
        />

        <!-- Info section header (shown in advanced mode; contains the Basic/Advanced toggle when adding) -->
        <div v-if="builderMode === 'advanced'" class="flex items-center gap-2">
          <span class="text-xs font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Info</span>
          <div class="flex-1 h-px bg-gray-700"></div>
          <div v-if="!editingDef" class="flex rounded-md overflow-hidden border border-gray-700 flex-shrink-0 text-xs">
            <button type="button" class="px-3 py-1.5 transition-colors bg-darker text-gray-400 hover:text-gray-200"
              @click="builderMode = 'basic'">Basic</button>
            <button type="button" class="px-3 py-1.5 transition-colors bg-indigo-700 text-white"
              @click="builderMode = 'advanced'">Advanced</button>
          </div>
        </div>

        <!-- Advanced form fields -->
        <template v-if="builderMode === 'advanced'">

        <!-- Name -->
        <div class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/></svg>
            Name
          </span>
          <input
            v-model="form.name"
            type="text"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            placeholder="Report name"
          />
        </div>

        <!-- Description -->
        <div class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h8"/></svg>
            Description (optional)
          </span>
          <textarea
            v-model="form.description"
            rows="2"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-xs text-gray-400 focus:outline-none focus:border-blue-500 resize-none"
            placeholder="What does this report show?"
          />
        </div>

        <!-- Share toggle (new reports only, multi-account) -->
        <div v-if="!editingDef && knownAccountCount > 1" class="flex items-center gap-2">
          <input id="shareWithAllCheck" type="checkbox" class="ext-opt-check" v-model="shareWithAll" />
          <label for="shareWithAllCheck" class="text-xs text-gray-400 cursor-pointer select-none">Share with all accounts</label>
        </div>
        <!-- Shared report indicator (editing an existing shared report) -->
        <div v-if="editingDef?.accountId === '__global__'" class="flex items-center gap-1">
          <span class="text-xs px-1.5 py-0.5 rounded border border-indigo-700 bg-indigo-900/40 text-indigo-400">Shared report</span>
        </div>

        <!-- Data section header -->
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Data</span>
          <div class="flex-1 h-px bg-gray-700"></div>
        </div>

        <!-- Subject + Mode row -->
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4"/></svg>
              Subject
            </span>
            <select
              v-model="form.subject"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
              @change="onSubjectChange"
            >
              <option value="ships">Ships</option>
              <option value="artifacts">Artifacts</option>
            </select>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg>
              Mode
            </span>
            <select
              v-model="form.mode"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
              @change="onSubjectOrModeChange"
            >
              <option value="aggregate">Aggregate</option>
              <option value="time_series">Time Series</option>
            </select>
          </div>
        </div>

        <!-- Family weight (artifacts only) -->
        <div v-if="form.subject === 'artifacts'" class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3"/></svg>
            Family weight (optional)
          </span>
          <select
            v-model="form.familyWeight"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
          >
            <option value="">None (raw count)</option>
            <option v-for="fam in familyList" :key="fam.id" :value="fam.id">{{ fam.name }}</option>
          </select>
          <p v-if="form.familyWeight" class="text-xs text-gray-500 mt-0.5">Drops weighted by T1-equivalent crafting cost</p>
        </div>

        <!-- Display mode + Group by row -->
        <div class="grid grid-cols-2 gap-3">
          <div v-if="!(form.mode === 'time_series' && form.secondaryGroupBy)" class="flex flex-col gap-1">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/></svg>
              Display
            </span>
            <select
              v-model="form.displayMode"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
              @change="form.chartType = ''"
            >
              <template v-if="form.secondaryGroupBy">
                <option value="heatmap">Heatmap</option>
                <option value="grouped_bar">Grouped Bar</option>
              </template>
              <template v-else-if="form.mode === 'time_series'">
                <option value="line">Line chart</option>
              </template>
              <template v-else>
                <option value="bar">Bar chart</option>
                <option value="line">Line chart</option>
                <option value="pie">Pie chart</option>
                <option value="grid">Table</option>
              </template>
            </select>
          </div>
          <div class="flex flex-col gap-1" :class="{ 'col-span-2': form.mode === 'time_series' && form.secondaryGroupBy }">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A2 2 0 013 12V7a4 4 0 014-4z"/></svg>
              Group by
            </span>
            <select
              v-model="form.groupBy"
              class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            >
              <option v-for="opt in groupByOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </div>
        </div>

        <!-- Chart style (line and bar charts) -->
        <div v-if="form.displayMode === 'line' || form.displayMode === 'bar'" class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z"/></svg>
            Style
          </span>
          <select
            v-model="form.chartType"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
          >
            <template v-if="form.displayMode === 'line'">
              <option value="">Filled area</option>
              <option value="line">Line only</option>
            </template>
            <template v-else-if="form.displayMode === 'bar'">
              <option value="">Default order</option>
              <option value="bar_desc">Sorted descending</option>
              <option value="bar_asc">Sorted ascending</option>
            </template>
          </select>
        </div>

        <!-- Secondary Group By -->
        <div class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 6H21M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/></svg>
            Secondary Group By
          </span>
          <select
            v-model="form.secondaryGroupBy"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
            @change="onSecondaryGroupByChange"
          >
            <option value="">None (1D)</option>
            <optgroup label="Mission">
              <option v-for="opt in shipMissionAggregateOptions.filter(o => o.value !== form.groupBy)" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </optgroup>
            <optgroup label="Artifact">
              <option v-for="opt in artifactAggregateOptions.filter(o => o.value !== form.groupBy)" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </optgroup>
          </select>
          <p v-if="form.secondaryGroupBy" class="text-xs text-gray-500 mt-0.5">Counting: {{ inferredSubjectLabel }}</p>
        </div>

        <!-- Time bucket (time_series only) -->
        <div v-if="form.mode === 'time_series'" class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
              Time bucket
            </span>
            <select
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

        <!-- Value filter -->
        <div class="flex flex-col gap-1">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"/></svg>
            Filter results where value
          </span>
          <div class="flex items-center gap-1.5">
            <select
              id="rb-value-filter-op"
              v-model="form.valueFilterOp"
              class="text-xs bg-darker border border-gray-700 rounded px-1 py-1 text-gray-300 focus:outline-none focus:border-indigo-500 w-20"
            >
              <option value="">Off</option>
              <option value=">">&gt;</option>
              <option value="<">&lt;</option>
              <option value=">=">&gt;=</option>
              <option value="<=">&lt;=</option>
              <option value="=">=</option>
              <option value="!=">!=</option>
            </select>
            <input
              v-if="form.valueFilterOp"
              v-model.number="form.valueFilterThreshold"
              type="number"
              step="any"
              class="text-xs bg-darker border border-gray-700 rounded px-2 py-1 text-gray-300 focus:outline-none focus:border-indigo-500 flex-1 min-w-0"
              placeholder="0"
            />
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

          <!-- OR Groups -->
          <div class="mt-2">
            <div class="flex items-center justify-between mb-1">
              <span class="text-xs text-gray-500 font-medium">OR Groups</span>
              <button
                type="button"
                class="text-xs text-indigo-400 hover:text-indigo-300"
                @click="addOrGroup"
              >+ Add OR Group</button>
            </div>
            <div v-for="(group, gIdx) in orGroups" :key="gIdx" class="mb-2 border border-gray-700 rounded p-2">
              <div class="flex items-center justify-between mb-1">
                <span class="text-xs text-gray-500">Group {{ gIdx + 1 }}</span>
                <button
                  type="button"
                  class="text-xs text-red-500 hover:text-red-400"
                  @click="removeOrGroup(gIdx)"
                >Remove</button>
              </div>
              <div v-for="(_cond, cIdx) in group" :key="cIdx" class="flex gap-1 mb-1">
                <select
                  :value="orGroups[gIdx][cIdx].topLevel"
                  class="text-xs bg-darker border border-gray-700 rounded px-1 py-1 text-gray-300 focus:outline-none flex-1"
                  @change="updateOrCondition(gIdx, cIdx, { topLevel: ($event.target as HTMLSelectElement).value, op: '', val: '' })"
                >
                  <option value="">Field...</option>
                  <optgroup label="Mission">
                    <option v-for="f in missionFilterFields" :key="f.value" :value="f.value">{{ f.label }}</option>
                  </optgroup>
                  <optgroup v-if="form.subject === 'artifacts'" label="Artifact">
                    <option v-for="f in artifactFilterFields" :key="f.value" :value="f.value">{{ f.label }}</option>
                  </optgroup>
                </select>
                <template v-if="isBoolField(orGroups[gIdx][cIdx].topLevel)">
                  <select
                    class="text-xs bg-darker border border-gray-700 rounded px-1 py-1 text-gray-300 focus:outline-none w-14 opacity-60 cursor-not-allowed"
                    disabled
                  >
                    <option>is</option>
                  </select>
                  <select
                    :value="orGroups[gIdx][cIdx].op"
                    class="text-xs bg-darker border border-gray-700 rounded px-1 py-1 text-gray-300 focus:outline-none"
                    @change="updateOrCondition(gIdx, cIdx, { op: ($event.target as HTMLSelectElement).value })"
                  >
                    <option value="true">True</option>
                    <option value="false">False</option>
                  </select>
                </template>
                <template v-else>
                  <select
                    :value="orGroups[gIdx][cIdx].op"
                    class="text-xs bg-darker border border-gray-700 rounded px-1 py-1 text-gray-300 focus:outline-none w-14"
                    :disabled="getOpsForField(orGroups[gIdx][cIdx].topLevel).length <= 1"
                    :class="getOpsForField(orGroups[gIdx][cIdx].topLevel).length <= 1 ? 'opacity-60 cursor-not-allowed' : ''"
                    @change="updateOrCondition(gIdx, cIdx, { op: ($event.target as HTMLSelectElement).value })"
                  >
                    <option v-for="op in getOpsForField(orGroups[gIdx][cIdx].topLevel)" :key="op.value" :value="op.value">{{ op.label }}</option>
                  </select>
                  <input
                    :value="orGroups[gIdx][cIdx].val"
                    :type="isDateField(orGroups[gIdx][cIdx].topLevel) ? 'date' : 'text'"
                    class="text-xs bg-darker border border-gray-700 rounded px-2 py-1 text-gray-300 focus:outline-none flex-1 min-w-0"
                    @input="updateOrCondition(gIdx, cIdx, { val: ($event.target as HTMLInputElement).value })"
                  />
                </template>
                <button type="button" class="text-gray-500 hover:text-red-400 text-xs px-1" @click="removeFromOrGroup(gIdx, cIdx)">x</button>
              </div>
              <button
                type="button"
                class="text-xs text-gray-500 hover:text-gray-300"
                @click="addToOrGroup(gIdx)"
              >+ Condition</button>
            </div>
          </div>
        </div>

        <!-- Normalize by (aggregate only) -->
        <div v-if="form.mode === 'aggregate'" class="flex flex-col gap-2">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 11h.01M12 11h.01M15 11h.01M4 19h16a2 2 0 002-2V7a2 2 0 00-2-2H4a2 2 0 00-2 2v10a2 2 0 002 2z"/></svg>
            Normalize by (optional)
          </span>
          <select
            v-model="form.normalizeBy"
            class="bg-darker border border-gray-700 rounded px-2 py-1.5 text-sm text-gray-200 focus:outline-none focus:border-blue-500"
          >
            <template v-if="form.secondaryGroupBy">
              <option value="none">None (raw count)</option>
              <option value="row_pct">Row % (each row sums to 100)</option>
              <option value="col_pct">Column % (each column sums to 100)</option>
              <option value="global_pct">Global % (all cells sum to 100)</option>
              <template v-if="canNormalizePivotPerLaunch">
                <option value="launches">Per launch (per cell)</option>
                <option value="airtime">Per flight hour (per cell)</option>
              </template>
            </template>
            <template v-else>
              <option value="none">None (raw count)</option>
              <option value="launches">Per launch</option>
              <option value="airtime">Per flight hour</option>
            </template>
          </select>
        </div>

        <!-- Appearance section header -->
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Appearance</span>
          <div class="flex-1 h-px bg-gray-700"></div>
        </div>

        <!-- Chart color + Zero cell color (same row when both visible) -->
        <div v-if="form.displayMode !== 'grid'" class="flex flex-row gap-6 flex-wrap">
          <div class="flex flex-col gap-1">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01"/></svg>
              {{ form.displayMode === 'pie' ? 'Fallback color' : 'Chart color' }}
            </span>
            <ColorPicker v-model="form.color" />
          </div>
          <div v-if="form.displayMode === 'heatmap'" class="flex flex-col gap-1">
            <span class="text-xs text-gray-400 flex items-center gap-1">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01"/></svg>
              Zero cell color
            </span>
            <ColorPicker v-model="form.unfilledColor" />
          </div>
        </div>

        <!-- Per-slice / per-bar colors -->
        <div v-if="form.displayMode === 'pie' || form.displayMode === 'bar'" class="flex flex-col gap-2">
          <span class="text-xs text-gray-400 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01"/></svg>
            {{ form.displayMode === 'pie' ? 'Slice colors' : 'Bar colors' }}
          </span>
          <div v-if="chartLabels.length > 0" class="grid grid-cols-2 gap-1.5">
            <div
              v-for="label in chartLabels"
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
            <!-- 8x4 visual picker -->
            <div class="flex flex-col gap-0.5 self-start" @mouseleave="clearHover()">
              <div v-for="r in 4" :key="r" class="flex gap-0.5">
                <div
                  v-for="c in 8"
                  :key="c"
                  class="w-3.5 h-3.5 rounded-sm border cursor-pointer transition-colors"
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
          :disabled="!canSave || isSaving"
          @click="handleSave"
        >
          {{ isSaving ? 'Saving...' : 'Save' }}
        </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed, watch, ref, onMounted, nextTick } from 'vue'
import type { ReportDefinition, ReportFilterCondition, PossibleTarget, FamilyMeta } from '../types/bridge'
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

const {
  andConditions,
  orGroups,
  addAndCondition,
  removeAndCondition,
  updateAndCondition,
  addOrGroup,
  removeOrGroup,
  addToOrGroup,
  removeFromOrGroup,
  updateOrCondition,
  toReportFilters,
  fromReportFilters,
  clearFilters,
} = useReportFilters()

const possibleTargets = ref<PossibleTarget[]>([])
const familyList = ref<FamilyMeta[]>([])
const isDirty = ref(false)
const closeWarning = ref(false)
const builderMode = ref<'basic' | 'advanced'>('basic')
const shareWithAll = ref(false)
const knownAccountCount = ref(0)
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
    secondaryGroupBy: '',
    timeBucket: 'month',
    customBucketN: 3,
    customBucketUnit: 'month',
    gridW: 2,
    gridH: 2,
    color: '#6366f1',
    unfilledColor: '#1f2937',
    valueFilterOp: '',
    valueFilterThreshold: 0,
    normalizeBy: 'none',
    chartType: '',
    familyWeight: '',
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
      form.subject = def.subject === 'missions' ? 'ships' : def.subject
      form.mode = def.mode
      form.displayMode = def.displayMode
      form.groupBy = def.groupBy
      form.timeBucket = def.timeBucket || 'month'
      form.customBucketN = def.customBucketN || 3
      form.customBucketUnit = def.customBucketUnit || 'month'
      form.gridW = def.gridW || 2
      form.gridH = def.gridH || 2
      form.color = def.color || '#6366f1'
      form.unfilledColor = def.unfilledColor || '#1f2937'
      form.valueFilterOp = def.valueFilterOp || ''
      form.valueFilterThreshold = def.valueFilterThreshold || 0
      form.normalizeBy = def.normalizeBy || 'none'
      form.secondaryGroupBy = def.secondaryGroupBy || ''
      form.chartType = def.chartType || ''
      form.familyWeight = def.familyWeight || ''
      labelColorsMap.value = parseLabelColors(def.labelColors)
      fromReportFilters(def.filters)
    } else {
      Object.assign(form, makeBlankForm())
      labelColorsMap.value = {}
      clearFilters()
      shareWithAll.value = false
    }
    nextTick(() => { isDirty.value = false })
  },
  { immediate: true },
)

watch(form, () => { isDirty.value = true }, { deep: true })
watch(andConditions, () => { isDirty.value = true }, { deep: true })
watch(orGroups, () => { isDirty.value = true }, { deep: true })
watch(labelColorsMap, () => { isDirty.value = true }, { deep: true })

const chartLabels = computed(() => {
  if (form.displayMode !== 'pie' && form.displayMode !== 'bar') return []
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
  const idx = chartLabels.value.indexOf(label)
  const colors = autoSliceColors(form.color, chartLabels.value.length)
  return idx >= 0 ? (colors[idx] ?? form.color) : form.color
}

const subjectLabel = computed(() => {
  const map: Record<string, string> = { ships: 'Ships', artifacts: 'Artifacts' }
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

const artifactDimensions = new Set(['artifact_name', 'rarity', 'tier', 'spec_type'])

const inferredSubjectLabel = computed(() => {
  if (!form.secondaryGroupBy) return ''
  const eitherArtifact = artifactDimensions.has(form.groupBy) || artifactDimensions.has(form.secondaryGroupBy) || !!form.familyWeight
  return eitherArtifact ? 'Artifact drops' : 'Missions'
})

function onSecondaryGroupByChange() {
  if (form.secondaryGroupBy) {
    if (form.mode === 'time_series') {
      form.displayMode = 'multi_line'
    } else if (!['heatmap', 'grouped_bar'].includes(form.displayMode)) {
      form.displayMode = 'heatmap'
    }
    if (!form.familyWeight) {
      if (artifactDimensions.has(form.groupBy) || artifactDimensions.has(form.secondaryGroupBy)) {
        form.subject = 'artifacts'
      } else {
        form.subject = 'ships'
      }
    }
  } else {
    form.displayMode = form.mode === 'time_series' ? 'line' : 'bar'
  }
}

function getOpsForField(field: string): { value: string, label: string }[] {
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

const groupByOptions = computed(() => {
  if (form.mode === 'time_series') return timeSeriesOption
  if (form.subject === 'artifacts') return [...shipMissionAggregateOptions, ...artifactAggregateOptions]
  return shipMissionAggregateOptions
})

const canNormalizePivotPerLaunch = computed(() => {
  if (!form.secondaryGroupBy) return false
  return !artifactDimensions.has(form.groupBy) && !artifactDimensions.has(form.secondaryGroupBy)
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

function resetAggregateDisplayAndNormalize() {
  if (form.secondaryGroupBy) {
    if (!['heatmap', 'grouped_bar'].includes(form.displayMode)) {
      form.displayMode = 'heatmap'
    }
    const perCellOk = !artifactDimensions.has(form.groupBy) && !artifactDimensions.has(form.secondaryGroupBy)
    if (['launches', 'airtime'].includes(form.normalizeBy) && !perCellOk) {
      form.normalizeBy = 'none'
    }
  } else {
    if (!['bar', 'pie', 'grid', 'line'].includes(form.displayMode)) {
      form.displayMode = 'bar'
    }
    if (['row_pct', 'col_pct', 'global_pct'].includes(form.normalizeBy)) {
      form.normalizeBy = 'none'
    }
  }
}

function onSubjectOrModeChange() {
  const opts = groupByOptions.value
  if (!opts.some(o => o.value === form.groupBy) && opts.length > 0) {
    form.groupBy = opts[0].value
  }
  if (form.mode === 'time_series') {
    form.displayMode = form.secondaryGroupBy ? 'multi_line' : 'line'
    form.normalizeBy = 'none'
  } else {
    resetAggregateDisplayAndNormalize()
  }
}

function onSubjectChange() {
  clearFilters()
  if (form.subject !== 'artifacts') form.familyWeight = ''
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

const isSaving = ref(false)

function resolveAccountId(): string {
  if (props.editingDef?.accountId === '__global__') return '__global__'
  if (shareWithAll.value) return '__global__'
  return props.accountId
}

function handleSave() {
  isSaving.value = true
  isDirty.value = false
  closeWarning.value = false
  const def: ReportDefinition = {
    id: props.editingDef?.id ?? '',
    accountId: resolveAccountId(),
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
    gridW: clamp(form.gridW, 1, 8),
    gridH: clamp(form.gridH, 1, 8),
    color: form.color,
    weight: '',
    sortOrder: 0,
    createdAt: 0,
    updatedAt: 0,
    valueFilterOp: form.valueFilterOp,
    valueFilterThreshold: form.valueFilterThreshold,
    normalizeBy: form.mode === 'aggregate' ? form.normalizeBy : 'none',
    secondaryGroupBy: form.secondaryGroupBy,
    chartType: ['line', 'bar'].includes(form.displayMode) ? (form.chartType || '') : '',
    groupId: props.editingDef?.groupId ?? '',
    labelColors: ['pie', 'bar'].includes(form.displayMode) && Object.keys(labelColorsMap.value).length > 0
      ? JSON.stringify(labelColorsMap.value)
      : '',
    unfilledColor: form.displayMode === 'heatmap' ? (form.unfilledColor || '') : '',
    familyWeight: form.subject === 'artifacts' ? (form.familyWeight || '') : '',
  }
  emit('saved', def)
  setTimeout(() => { isSaving.value = false }, 4000)
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
  const [, accounts] = await Promise.all([
    globalThis.getPossibleTargets().then(v => { possibleTargets.value = v }),
    globalThis.knownAccounts(),
    globalThis.getFamilyList().then(json => { familyList.value = JSON.parse(json) as FamilyMeta[] }),
  ])
  knownAccountCount.value = accounts.length
  nextTick(() => {
    const el = document.querySelector('.overlay-report') as HTMLElement | null
    el?.focus()
  })
})
</script>
