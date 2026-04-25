<template>
  <div class="flex-1 min-h-0 overflow-y-auto py-2 px-3 flex flex-col gap-3">
    <div class="flex items-center justify-between">
      <span class="text-sm font-semibold text-gray-300">Reports</span>
      <div class="flex items-center gap-2">
        <span v-if="importError" class="text-xs text-red-400">{{ importError }}</span>
        <button
          type="button"
          class="text-xs px-2 py-1 rounded border border-gray-600 text-gray-400 hover:text-gray-300"
          @click="handleImport"
        >Import</button>
        <button
          type="button"
          class="text-xs px-2 py-1 rounded border"
          :class="editMode
            ? 'border-indigo-500 text-indigo-400 hover:text-indigo-300'
            : 'border-gray-600 text-gray-400 hover:text-gray-300'"
          @click="editMode = !editMode"
        >
          {{ editMode ? 'Done' : 'Edit' }}
        </button>
      </div>
    </div>

    <div v-if="reports.length === 0" class="text-xs text-gray-500 text-center mt-8">
      No reports yet. {{ editMode ? 'Add one below.' : 'Enable edit mode to add one.' }}
    </div>

    <div
      class="grid gap-3"
      style="grid-template-columns: repeat(4, 1fr);"
    >
      <div
        v-for="(def, index) in reports"
        :key="def.id"
        :draggable="editMode"
        :style="{
          gridColumn: `span ${Math.min(Math.max(def.gridW, 1), 4)}`,
          gridRow: `span ${Math.min(Math.max(def.gridH, 1), 4)}`,
        }"
        :class="{
          'opacity-40': draggingIndex === index,
          'ring-2 ring-blue-500 rounded-lg': dragOverIndex === index && draggingIndex !== index,
        }"
        @dragstart="onDragStart(index, $event)"
        @dragend="onDragEnd"
        @dragover.prevent="onDragOver(index)"
        @drop.prevent="onDrop(index)"
      >
        <ReportCard
          :def="def"
          :result="results[def.id] ?? null"
          :edit-mode="editMode"
          :running="runningIds.has(def.id)"
          @run="handleRun(def.id)"
          @edit="openEditor(def)"
          @delete="handleDelete(def.id)"
          @export="downloadReportJson(def.id, def.name)"
        />
      </div>
    </div>

    <div v-if="editMode" class="flex justify-center mt-1">
      <button
        type="button"
        class="text-xs px-3 py-1.5 rounded border border-dashed border-gray-600 text-gray-400 hover:text-gray-200 hover:border-gray-400"
        @click="openEditor(null)"
      >
        + Add Report
      </button>
    </div>

    <ReportBuilderPanel
      v-if="builderOpen"
      :account-id="accountId"
      :editing-def="editingDef"
      @saved="handleSaved"
      @close="builderOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import type { ReportDefinition, ReportResult } from '../types/bridge'
import { AppState } from '../types/bridge'
import { useReports } from '../composables/useReports'
import { useAppState } from '../composables/useAppState'
import { downloadReportJson, readImportFile } from '../utils/reportIO'
import ReportCard from './ReportCard.vue'
import ReportBuilderPanel from './ReportBuilderPanel.vue'

const props = defineProps<{
  accountId: string
}>()

const { reports, loadReports, deleteReport, reorderReports, executeReport, createReport, updateReport } = useReports()
const { appState } = useAppState()

const editMode = ref(false)
const builderOpen = ref(false)
const editingDef = ref<ReportDefinition | null>(null)
const results = ref<Record<string, ReportResult>>({})
const runningIds = ref(new Set<string>())
const draggingIndex = ref<number | null>(null)
const dragOverIndex = ref<number | null>(null)
const importError = ref<string | null>(null)

onMounted(async () => {
  await loadReports(props.accountId)
  for (const def of reports.value) {
    void handleRun(def.id)
  }
})

watch(appState, (state) => {
  if (state === AppState.Success) {
    for (const def of reports.value) {
      void handleRun(def.id)
    }
  }
})

function openEditor(def: ReportDefinition | null) {
  editingDef.value = def
  builderOpen.value = true
}

async function handleRun(id: string) {
  runningIds.value = new Set([...runningIds.value, id])
  try {
    const result = await executeReport(id)
    if (result) {
      results.value = { ...results.value, [id]: result }
    }
  } finally {
    const next = new Set(runningIds.value)
    next.delete(id)
    runningIds.value = next
  }
}

async function handleDelete(id: string) {
  await deleteReport(id)
}

async function handleImport() {
  importError.value = null
  const json = await readImportFile()
  if (!json) return
  const newId = await globalThis.importReport(props.accountId, json)
  if (!newId) {
    importError.value = 'Import failed - invalid file'
    setTimeout(() => { importError.value = null }, 3000)
    return
  }
  await loadReports(props.accountId)
  void handleRun(newId)
}

function onDragStart(index: number, event: DragEvent) {
  draggingIndex.value = index
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
  }
}

function onDragEnd() {
  draggingIndex.value = null
  dragOverIndex.value = null
}

function onDragOver(index: number) {
  if (draggingIndex.value !== null && index !== draggingIndex.value) {
    dragOverIndex.value = index
  }
}

async function onDrop(targetIndex: number) {
  const from = draggingIndex.value
  draggingIndex.value = null
  dragOverIndex.value = null
  if (from === null || from === targetIndex) return
  const ids = reports.value.map(r => r.id)
  const [moved] = ids.splice(from, 1)
  ids.splice(targetIndex, 0, moved)
  await reorderReports(ids)
}

async function handleSaved(def: ReportDefinition) {
  const isNew = !reports.value.some(r => r.id === def.id)
  let runId: string | null = null
  if (isNew) {
    const saved = await createReport(def)
    runId = saved?.id ?? null
  } else {
    const ok = await updateReport(def)
    runId = ok ? def.id : null
  }
  builderOpen.value = false
  if (runId) {
    void handleRun(runId)
  }
}
</script>
