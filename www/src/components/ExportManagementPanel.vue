<template>
  <div>
    <div class="mt-0_5rem">
      <span class="section-heading">Export Pruning</span><br />
      <div class="mt-0_5rem pl-0_5rem flex flex-row items-center gap-2 flex-wrap">
        <label class="text-sm text-gray-400" for="keepCountInput">Keep</label>
        <input
          id="keepCountInput"
          type="number"
          class="number-input text-sm bg-darkest"
          style="width: 4rem"
          :value="keepCount"
          min="0"
          max="50"
          placeholder="0"
          @change="onKeepCountChange"
        />
        <span class="text-sm text-gray-400">most recent exports per player (0 = unlimited)</span>
      </div>
      <div v-if="toPrune.length > 0" class="mt-0_5rem pl-0_5rem flex flex-col items-start gap-1">
        <button
          type="button"
          class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-orange-700 !text-white hover:!bg-orange-600 !border-orange-500"
          :disabled="pruning"
          @click="doPruneNow"
        >{{ pruning ? 'Pruning...' : 'Prune Now' }}</button>
        <span class="text-xs text-gray-500 italic">Removes {{ pruneFileCount }} file(s), freeing {{ formattedSize(pruneBytes) }}</span>
      </div>
      <div v-if="pruneError" class="mt-0_25rem pl-0_5rem text-xs text-red-400">{{ pruneError }}</div>
    </div>

    <div class="mt-1rem">
      <span class="section-heading">Export Files</span><br />
      <div v-if="exportGroups.length === 0" class="mt-0_5rem pl-0_5rem text-xs text-gray-500 italic">
        No export files found.
      </div>
      <div v-else class="mt-0_5rem">
        <div v-for="group in exportGroups" :key="group.eid" class="mb-1">
          <button
            type="button"
            class="group w-full flex items-center gap-1 text-left text-xs text-gray-400 hover:text-gray-300 py-0.5"
            @click="toggleCollapse(group.eid)"
          >
            <span class="text-gray-500 w-3">{{ collapsed.has(group.eid) ? '▶' : '▼' }}</span>
            <span v-if="group.nickname" class="font-medium group-hover:brightness-125" :style="group.accountColor ? { color: '#' + group.accountColor } : {}">{{ group.nickname }}</span>
            <span class="font-mono">
              <template v-if="screenshotSafety">EI<span class="inline-block bg-gray-500 rounded-sm align-middle" style="width:5rem;height:0.75em;"></span></template>
              <template v-else>{{ group.eid }}</template>
            </span>
            <span class="text-gray-500 ml-1">· {{ group.pairs.length }} export(s) · {{ formattedSize(groupSize(group)) }}</span>
          </button>
          <div v-if="!collapsed.has(group.eid)" class="pl-4 mt-1">
            <table class="text-xs">
              <thead>
                <tr class="text-gray-500">
                  <th class="text-left pr-4 font-normal pb-0.5">Date</th>
                  <th class="text-left pr-4 font-normal pb-0.5">CSV</th>
                  <th class="text-left font-normal pb-0.5">XLSX</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="pair in group.pairs" :key="pair.timestamp" class="text-gray-400">
                  <td class="pr-4 py-0.5">{{ pair.displayDate }}</td>
                  <td class="pr-4 py-0.5">
                    <label v-if="pair.csvPath" class="flex items-center gap-1 cursor-pointer">
                      <input
                        type="checkbox"
                        class="ext-opt-check"
                        :checked="selectedPaths.has(pair.csvPath)"
                        @change="togglePath(pair.csvPath)"
                      />
                      <span>{{ formattedSize(pair.csvSize) }}</span>
                    </label>
                    <span v-else class="text-gray-600">-</span>
                  </td>
                  <td class="py-0.5">
                    <label v-if="pair.xlsxPath" class="flex items-center gap-1 cursor-pointer">
                      <input
                        type="checkbox"
                        class="ext-opt-check"
                        :checked="selectedPaths.has(pair.xlsxPath)"
                        @change="togglePath(pair.xlsxPath)"
                      />
                      <span>{{ formattedSize(pair.xlsxSize) }}</span>
                    </label>
                    <span v-else class="text-gray-600">-</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="mt-0_5rem pl-0_5rem flex flex-row items-center gap-2 flex-wrap">
          <span class="text-xs text-gray-500">{{ selectedPaths.size }} file(s) selected · {{ formattedSize(selectedBytes) }}</span>
          <button
            type="button"
            class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-red-900 !text-red-200 hover:!bg-red-800 !border-red-700"
            :disabled="selectedPaths.size === 0 || deleting"
            @click="doDeleteSelected"
          >{{ deleting ? 'Deleting...' : 'Delete selected' }}</button>
        </div>
        <div v-if="deleteError" class="mt-0_25rem pl-0_5rem text-xs text-red-400">{{ deleteError }}</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { screenshotSafety } from '../composables/useSettings'
import type { ExportGroup, ExportFilePair } from '../types/bridge'

const exportGroups = ref<ExportGroup[]>([])
const keepCount = ref(0)
const selectedPaths = ref(new Set<string>())
const collapsed = ref(new Set<string>())
const deleteError = ref('')
const pruneError = ref('')
const pruning = ref(false)
const deleting = ref(false)

const toPrune = computed<ExportFilePair[]>(() => {
  if (keepCount.value <= 0) return []
  const result: ExportFilePair[] = []
  for (const group of exportGroups.value) {
    if (group.pairs.length > keepCount.value) {
      result.push(...group.pairs.slice(keepCount.value))
    }
  }
  return result
})

const pruneFileCount = computed(() =>
  toPrune.value.reduce((n, p) => n + (p.csvPath ? 1 : 0) + (p.xlsxPath ? 1 : 0), 0),
)

const pruneBytes = computed(() =>
  toPrune.value.reduce((sum, p) => sum + p.csvSize + p.xlsxSize, 0),
)

const selectedBytes = computed(() => {
  let total = 0
  for (const group of exportGroups.value) {
    for (const pair of group.pairs) {
      if (pair.csvPath && selectedPaths.value.has(pair.csvPath)) total += pair.csvSize
      if (pair.xlsxPath && selectedPaths.value.has(pair.xlsxPath)) total += pair.xlsxSize
    }
  }
  return total
})

function formattedSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`
}

function groupSize(group: ExportGroup): number {
  return group.pairs.reduce((sum, p) => sum + p.csvSize + p.xlsxSize, 0)
}

function toggleCollapse(eid: string) {
  const next = new Set(collapsed.value)
  if (next.has(eid)) next.delete(eid)
  else next.add(eid)
  collapsed.value = next
}

function togglePath(path: string) {
  const next = new Set(selectedPaths.value)
  if (next.has(path)) next.delete(path)
  else next.add(path)
  selectedPaths.value = next
}

async function loadFiles() {
  const raw = await globalThis.listExportFiles()
  exportGroups.value = JSON.parse(raw) as ExportGroup[]
}

async function onKeepCountChange(e: Event) {
  const val = Number((e.target as HTMLInputElement).value)
  keepCount.value = Math.max(0, Math.min(50, Number.isNaN(val) ? 0 : val))
  await globalThis.setExportKeepCount(keepCount.value)
}

async function doPruneNow() {
  pruneError.value = ''
  pruning.value = true
  const raw = await globalThis.pruneOldExports()
  pruning.value = false
  const result = JSON.parse(raw) as { deleted: number; freedBytes: number; error: string }
  if (result.error) {
    pruneError.value = result.error
    return
  }
  selectedPaths.value = new Set()
  await loadFiles()
}

async function doDeleteSelected() {
  deleteError.value = ''
  deleting.value = true
  const paths = [...selectedPaths.value]
  const errMsg = await globalThis.deleteExportFiles(JSON.stringify(paths))
  deleting.value = false
  if (errMsg) {
    deleteError.value = errMsg
    return
  }
  selectedPaths.value = new Set()
  await loadFiles()
}

onMounted(async () => {
  const [, kc] = await Promise.all([
    loadFiles(),
    globalThis.getExportKeepCount(),
  ])
  keepCount.value = kc
  collapsed.value = new Set(exportGroups.value.map(g => g.eid))
})
</script>
