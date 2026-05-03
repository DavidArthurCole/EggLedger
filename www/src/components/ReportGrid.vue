<template>
  <div class="flex-1 min-h-0 overflow-y-auto py-2 px-3 flex flex-col gap-3">
    <span v-if="exportAllMessage" class="text-xs text-gray-400 self-end max-w-48 truncate" :title="exportAllMessage">{{ exportAllMessage }}</span>
    <span v-if="importError" class="text-xs text-red-400 self-end">{{ importError }}</span>

    <div v-if="reports.length === 0" class="text-xs text-gray-500 text-center mt-8">
      No reports yet. Click + New Report to add one.
    </div>

    <div
      ref="gridRef"
      class="grid gap-3 relative"
      :style="`grid-template-columns: repeat(8, 1fr); grid-auto-rows: ${rowHeight}px;`"
      :class="{ 'ring-1 ring-blue-500/40 rounded-lg': draggingIndex !== null && dragOverIndex === displayedReports.length }"
      @dragover.prevent="onGridDragOver($event)"
      @drop.prevent="onGridDrop($event)"
    >
      <div
        v-for="(def, index) in displayedReports"
        :key="def.id"
        :data-card-index="index"
        :draggable="editMode"
        :style="{
          gridColumn: `span ${Math.min(Math.max(def.gridW, 1), 8)}`,
          gridRow: `span ${Math.min(Math.max(def.gridH, 1), 8)}`,
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
          :menno-result="mennoResults[def.id] ?? null"
          :edit-mode="editMode"
          :running="runningIds.has(def.id)"
          :stale="isStale(def.id, def.weight)"
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
        style="grid-column: 1 / -1;"
        :class="dragOverIndex === displayedReports.length ? 'border-blue-500 bg-blue-500/10 text-blue-400' : 'border-gray-700 text-gray-600'"
        @dragover.prevent="onDragOver(displayedReports.length)"
        @drop.prevent="onDrop(displayedReports.length)"
      >Move here</div>

      <template v-if="editMode && draggingIndex !== null">
        <div
          v-for="(zone, zi) in emptyDropZones"
          :key="`zone-${zi}`"
          class="rounded-lg border-2 border-dashed transition-colors pointer-events-auto"
          :class="dropZoneOverIdx === zi ? 'border-blue-500 bg-blue-500/10' : 'border-gray-700/60'"
          :style="{
            gridColumn: `${zone.colStart} / ${zone.colEnd}`,
            gridRow: `${zone.rowStart} / ${zone.rowStart + 1}`,
            position: 'absolute',
            inset: '0',
          }"
          @dragenter="onZoneDragEnter(zi)"
          @dragover.prevent
          @dragleave="onZoneDragLeave"
          @drop.prevent="onDropToZone(zone)"
        />
      </template>
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
      :editing-result-labels="editingDef && results[editingDef.id] && !results[editingDef.id].is2D ? results[editingDef.id].labels : []"
      @saved="handleSaved"
      @close="builderOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
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

const { reports, loadReports, deleteReport, reorderReports, executeReport, executeMennoComparison, createReport, updateReport } = useReports()
const { groups, loadGroups } = useReportGroups()
const { appState } = useAppState()

const editMode = computed(() => props.editMode ?? false)
const builderOpen = ref(false)
const editingDef = ref<ReportDefinition | null>(null)
const results = ref<Record<string, ReportResult>>({})
const mennoResults = ref<Record<string, ReportResult>>({})
const runningIds = ref(new Set<string>())
const refreshCycle = ref(0)
const resultCycles = ref<Record<string, number>>({})
const draggingIndex = ref<number | null>(null)
const dragOverIndex = ref<number | null>(null)

interface EmptyZone {
  colStart: number
  colEnd: number
  rowStart: number
  insertAfter: number
}
const emptyDropZones = ref<EmptyZone[]>([])
const dropZoneOverIdx = ref<number | null>(null)
const importError = ref<string | null>(null)
const exportAllMessage = ref<string | null>(null)

const gridRef = ref<HTMLElement | null>(null)
const rowHeight = ref(200)
let gridObs: ResizeObserver | null = null

onMounted(() => {
  const update = () => {
    if (!gridRef.value) return
    rowHeight.value = Math.max(80, Math.floor((gridRef.value.clientWidth - (GRID_COLS - 1) * GRID_GAP) / GRID_COLS))
  }
  gridObs = new ResizeObserver(update)
  if (gridRef.value) gridObs.observe(gridRef.value)
  update()
})

onUnmounted(() => { gridObs?.disconnect() })

const displayedReports = computed(() => {
  if (props.groupFilter == null) return reports.value
  return reports.value.filter(r => r.groupId === props.groupFilter)
})

onMounted(() => {
  for (const def of reports.value) {
    handleRun(def.id)
  }
})

watch(appState, (state) => {
  if (state === AppState.Success) {
    refreshCycle.value++
    for (const def of reports.value) {
      if (def.weight !== 'HEAVY') {
        handleRun(def.id)
      } else if (resultCycles.value[def.id] !== undefined) {
        resultCycles.value = { ...resultCycles.value, [def.id]: refreshCycle.value }
      }
    }
  }
})

function openEditor(def: ReportDefinition | null) {
  editingDef.value = def
  builderOpen.value = true
}

async function handleRun(id: string) {
  runningIds.value = new Set([...runningIds.value, id])
  const cycleAtStart = refreshCycle.value
  try {
    const result = await executeReport(id)
    if (result) {
      results.value = { ...results.value, [id]: result }
      resultCycles.value = { ...resultCycles.value, [id]: cycleAtStart }
      const def = reports.value.find(r => r.id === id)
      if (
        def?.mennoEnabled &&
        result.is2D &&
        result.rawRowLabels?.length &&
        result.rawColLabels?.length
      ) {
        executeMennoComparison(id, result.rawRowLabels, result.rawColLabels).then(mennoResult => {
          if (mennoResult) {
            mennoResults.value = { ...mennoResults.value, [id]: mennoResult }
          }
        })
      } else {
        const next = { ...mennoResults.value }
        delete next[id]
        mennoResults.value = next
      }
    }
  } finally {
    const next = new Set(runningIds.value)
    next.delete(id)
    runningIds.value = next
  }
}

function isStale(id: string, weight: string): boolean {
  if (weight !== 'HEAVY') return false
  return resultCycles.value[id] !== undefined && resultCycles.value[id] < refreshCycle.value
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

interface GridCardPos { col: number; row: number; w: number; h: number; idx: number }

const GRID_COLS = 8
const GRID_GAP = 12

function clampDims(gridW: number, gridH: number): { w: number; h: number } {
  return { w: Math.min(Math.max(gridW, 1), GRID_COLS), h: Math.min(Math.max(gridH, 1), 8) }
}

function markOccupied(occupied: Set<string>, col: number, row: number, w: number, h: number) {
  for (let rr = row; rr < row + h; rr++)
    for (let cc = col; cc < col + w; cc++) occupied.add(`${cc},${rr}`)
}

function cellsFit(occupied: Set<string>, c: number, r: number, w: number, h: number): boolean {
  for (let rr = r; rr < r + h; rr++)
    for (let cc = c; cc < c + w; cc++)
      if (occupied.has(`${cc},${rr}`)) return false
  return true
}

// Replicates CSS grid auto-placement (row direction, no dense).
function findPlacement(occupied: Set<string>, w: number, h: number, col: number, row: number): { col: number; row: number } {
  let c = col
  let r = row
  while (true) {
    if (c + w - 1 > GRID_COLS) { c = 1; r++ }
    if (cellsFit(occupied, c, r, w, h)) return { col: c, row: r }
    c++
  }
}

function simulatePlacement(defs: ReportDefinition[]): { col: number; row: number; occupied: Set<string> } {
  const occupied = new Set<string>()
  let col = 1
  let row = 1
  for (const def of defs) {
    const { w, h } = clampDims(def.gridW, def.gridH)
    const pos = findPlacement(occupied, w, h, col, row)
    col = pos.col
    row = pos.row
    markOccupied(occupied, col, row, w, h)
    col += w
  }
  return { col, row, occupied }
}

function buildOccupancyFromLayout(): { cardPositions: GridCardPos[]; occupied: Set<string> } {
  const cardPositions: GridCardPos[] = []
  const occupied = new Set<string>()
  let col = 1
  let row = 1
  for (let idx = 0; idx < displayedReports.value.length; idx++) {
    const def = displayedReports.value[idx]
    const { w, h } = clampDims(def.gridW, def.gridH)
    const pos = findPlacement(occupied, w, h, col, row)
    col = pos.col
    row = pos.row
    cardPositions.push({ col, row, w, h, idx })
    markOccupied(occupied, col, row, w, h)
    col += w
  }
  return { cardPositions, occupied }
}

function zoneInsertAfter(cardPositions: GridCardPos[], targetRow: number, runStart: number): number {
  const zoneFirst = (targetRow - 1) * GRID_COLS + runStart
  let best = -1
  for (const cp of cardPositions) {
    if ((cp.row - 1) * GRID_COLS + cp.col < zoneFirst) best = Math.max(best, cp.idx)
  }
  return best
}

function findInsertIndexForZone(zone: EmptyZone, fromIdx: number): number {
  const defs = displayedReports.value
  const draggedDef = defs[fromIdx]
  if (!draggedDef) return fromIdx

  const withoutDragged = defs.filter((_, i) => i !== fromIdx)
  const { w: dw, h: dh } = clampDims(draggedDef.gridW, draggedDef.gridH)

  for (let insertPos = 0; insertPos <= withoutDragged.length; insertPos++) {
    const { col, row, occupied } = simulatePlacement(withoutDragged.slice(0, insertPos))
    const pos = findPlacement(occupied, dw, dh, col, row)
    if (pos.col === zone.colStart && pos.row === zone.rowStart) return insertPos
  }

  // Fallback: use zone.insertAfter
  let insertAt = zone.insertAfter
  if (fromIdx <= insertAt) insertAt--
  return Math.max(insertAt + 1, 0)
}

function rowEmptyZones(
  r: number,
  occupied: Set<string>,
  cardPositions: GridCardPos[],
): EmptyZone[] {
  const zones: EmptyZone[] = []
  let runStart: number | null = null
  for (let c = 1; c <= GRID_COLS + 1; c++) {
    const isEmpty = c <= GRID_COLS && !occupied.has(`${c},${r}`)
    if (isEmpty && runStart === null) { runStart = c; continue }
    if (!isEmpty && runStart !== null) {
      zones.push({
        colStart: runStart,
        colEnd: c,
        rowStart: r,
        insertAfter: zoneInsertAfter(cardPositions, r, runStart),
      })
      runStart = null
    }
  }
  return zones
}

function computeEmptyZones() {
  if (draggingIndex.value === null) { emptyDropZones.value = []; return }
  const { cardPositions, occupied } = buildOccupancyFromLayout()
  if (cardPositions.length === 0) { emptyDropZones.value = []; return }
  const maxRow = Math.max(...cardPositions.map(p => p.row + p.h - 1))
  const zones: EmptyZone[] = []
  for (let r = 1; r <= maxRow; r++) zones.push(...rowEmptyZones(r, occupied, cardPositions))
  emptyDropZones.value = zones
}

let _dragleaveTimer: ReturnType<typeof setTimeout> | null = null

function onZoneDragEnter(zi: number) {
  if (_dragleaveTimer !== null) { clearTimeout(_dragleaveTimer); _dragleaveTimer = null }
  dropZoneOverIdx.value = zi
}

function onZoneDragLeave() {
  _dragleaveTimer = setTimeout(() => {
    dropZoneOverIdx.value = null
    _dragleaveTimer = null
  }, 60)
}

function onDragStart(index: number, event: DragEvent) {
  draggingIndex.value = index
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', String(index))
  }
  nextTick(() => computeEmptyZones())
}

function onDragEnd() {
  if (_dragleaveTimer !== null) { clearTimeout(_dragleaveTimer); _dragleaveTimer = null }
  draggingIndex.value = null
  dragOverIndex.value = null
  dropZoneOverIdx.value = null
  emptyDropZones.value = []
}

async function onDropToZone(zone: EmptyZone) {
  if (_dragleaveTimer !== null) { clearTimeout(_dragleaveTimer); _dragleaveTimer = null }
  const from = draggingIndex.value
  draggingIndex.value = null
  dragOverIndex.value = null
  dropZoneOverIdx.value = null
  emptyDropZones.value = []
  if (from === null) return
  const insertPos = findInsertIndexForZone(zone, from)
  const ids = reports.value.map(r => r.id)
  const [moved] = ids.splice(from, 1)
  ids.splice(insertPos, 0, moved)
  await reorderReports(ids)
}

function onGridDragOver(event: DragEvent) {
  if (draggingIndex.value === null) return
  if ((event.target as Element) === (event.currentTarget as Element)) {
    dragOverIndex.value = displayedReports.value.length
  }
}

function onGridDrop(event: DragEvent) {
  if (draggingIndex.value === null) return
  // Prefer the last-hovered zone — covers the case where dragleave fired just
  // before drop due to cursor landing on the gap between zone and card edges.
  if (dropZoneOverIdx.value !== null) {
    const zone = emptyDropZones.value[dropZoneOverIdx.value]
    if (zone) { onDropToZone(zone); return }
  }
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
    if (runId && props.groupFilter && def.accountId !== '__global__') {
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
