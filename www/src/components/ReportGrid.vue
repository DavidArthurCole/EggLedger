<template>
  <div class="flex-1 min-h-0 overflow-y-auto py-2 px-3 flex flex-col gap-3">
    <span v-if="exportAllMessage" class="text-xs text-gray-400 self-end max-w-48 truncate" :title="exportAllMessage">{{ exportAllMessage }}</span>
    <span v-if="importError" class="text-xs text-red-400 self-end">{{ importError }}</span>

    <div v-if="reports.length === 0" class="text-xs text-gray-500 text-center mt-8">
      No reports yet. Click + New Report to add one.
    </div>

    <div
      class="grid gap-3"
      style="grid-template-columns: repeat(4, 1fr); grid-auto-rows: 200px;"
      @dragover.prevent="onGridDragOver($event)"
      @drop.prevent="onGridDrop($event)"
    >
      <div
        v-for="(def, index) in displayedReports"
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
          :groups="editMode ? groups : []"
          @run="handleRun(def.id)"
          @edit="openEditor(def)"
          @copy="handleCopy(def)"
          @delete="handleDelete(def.id)"
          @export="downloadReportJson(def.id, def.name)"
          @set-group="handleSetGroup(def.id, $event)"
        />
      </div>
      <div
        v-if="editMode && draggingIndex !== null"
        class="rounded-lg border-2 border-dashed flex items-center justify-center text-xs transition-colors select-none"
        :class="dragOverIndex === displayedReports.length ? 'border-blue-500 bg-blue-500/10 text-blue-400' : 'border-gray-700 text-gray-600'"
        @dragover.prevent="onDragOver(displayedReports.length)"
        @drop.prevent="onDrop(displayedReports.length)"
      >Move here</div>
    </div>

    <div v-if="displayedReports.length === 0 && reports.length > 0" class="text-xs text-gray-500 text-center mt-8">
      No reports in this group.
    </div>

    <div class="flex justify-center mt-1">
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
      :editing-result-labels="editingDef && results[editingDef.id] ? results[editingDef.id].labels : []"
      @saved="handleSaved"
      @close="builderOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import type { ReportDefinition, ReportResult } from '../types/bridge'
import { AppState } from '../types/bridge'
import { useReports } from '../composables/useReports'
import { useReportGroups } from '../composables/useReportGroups'
import { useAppState } from '../composables/useAppState'
import { downloadReportJson, readImportFile } from '../utils/reportIO'
import ReportCard from './ReportCard.vue'
import ReportBuilderPanel from './ReportBuilderPanel.vue'

const props = defineProps<{
  accountId: string
  groupFilter?: string | null
  editMode?: boolean
}>()

const { reports, loadReports, deleteReport, reorderReports, executeReport, createReport, updateReport } = useReports()
const { groups, loadGroups } = useReportGroups()
const { appState } = useAppState()

const editMode = computed(() => props.editMode ?? false)
const builderOpen = ref(false)
const editingDef = ref<ReportDefinition | null>(null)
const results = ref<Record<string, ReportResult>>({})
const runningIds = ref(new Set<string>())
const draggingIndex = ref<number | null>(null)
const dragOverIndex = ref<number | null>(null)
const importError = ref<string | null>(null)
const exportAllMessage = ref<string | null>(null)

const displayedReports = computed(() => {
  if (props.groupFilter == null) return reports.value
  return reports.value.filter(r => r.groupId === props.groupFilter)
})

onMounted(async () => {
  await loadReports(props.accountId)
  await loadGroups(props.accountId)
  for (const def of reports.value) {
    handleRun(def.id)
  }
})

watch(appState, (state) => {
  if (state === AppState.Success) {
    for (const def of reports.value) {
      handleRun(def.id)
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

function makeCopyName(name: string): string {
  const existing = new Set(reports.value.map(r => r.name))
  const match = name.match(/^(.*)\s+\((\d+)\)$/)
  const base = match ? match[1] : name
  let n = match ? Number.parseInt(match[2]) : 0
  let candidate: string
  do {
    n++
    candidate = `${base} (${n})`
  } while (existing.has(candidate))
  return candidate
}

async function handleCopy(def: ReportDefinition) {
  const copy: ReportDefinition = { ...def, id: '', name: makeCopyName(def.name), sortOrder: 0, createdAt: 0, updatedAt: 0 }
  const saved = await createReport(copy)
  if (saved?.id) {
    if (def.groupId) await globalThis.setReportGroup(saved.id, def.groupId)
    handleRun(saved.id)
  }
}

async function handleExportAll() {
  exportAllMessage.value = null
  const defaultName = `reports-export-${Math.floor(Date.now() / 1000)}.json`
  const destPath = await globalThis.chooseSaveFilePath(defaultName)
  if (!destPath) return
  const path = await globalThis.exportAllReports(props.accountId, destPath)
  if (path) {
    exportAllMessage.value = `Saved to ${path}`
  } else {
    exportAllMessage.value = 'Export failed'
  }
  setTimeout(() => { exportAllMessage.value = null }, 5000)
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
  handleRun(newId)
}

async function handleSetGroup(reportId: string, groupId: string) {
  const ok = await globalThis.setReportGroup(reportId, groupId)
  if (ok) {
    reports.value = reports.value.map(r =>
      r.id === reportId ? { ...r, groupId } : r,
    )
  }
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

function onGridDragOver(event: DragEvent) {
  if (draggingIndex.value === null) return
  if ((event.target as Element) === (event.currentTarget as Element)) {
    dragOverIndex.value = displayedReports.value.length
  }
}

function onGridDrop(event: DragEvent) {
  if (draggingIndex.value === null) return
  if ((event.target as Element) === (event.currentTarget as Element)) {
    onDrop(displayedReports.value.length)
  }
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
    if (runId && props.groupFilter) {
      await globalThis.setReportGroup(runId, props.groupFilter)
      reports.value = reports.value.map(r =>
        r.id === runId ? { ...r, groupId: props.groupFilter! } : r,
      )
    }
  } else {
    const ok = await updateReport(def)
    runId = ok ? def.id : null
  }
  builderOpen.value = false
  if (runId) {
    handleRun(runId)
  }
}
</script>
