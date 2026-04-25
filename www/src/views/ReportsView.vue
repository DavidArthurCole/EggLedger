<template>
  <div class="view-layout overflow-hidden">
    <!-- Backfill progress -->
    <SegmentedProgressBar
      :active="!backfillStatus.done"
      :segments="backfillSegments"
      status-text="Indexing mission drop history for reports..."
      status-class="text-gray-400"
      :is-spinning="!backfillStatus.done"
    />


    <div v-if="!doesDataExist" class="flex-1 flex items-center justify-center">
      <p class="text-xs text-gray-500">No mission data found. Fetch data on the Ledger tab first.</p>
    </div>

    <div v-else class="flex-1 min-h-0 bg-darkest rounded-md overflow-hidden flex flex-col">
      <!-- Group tab bar -->
      <div v-if="loadedAccountId" class="flex items-center gap-1.5 px-2 pt-1.5 pb-0 flex-shrink-0 flex-wrap">
        <!-- All tab -->
        <button
          type="button"
          class="text-sm px-4 py-1.5 rounded-full transition-colors"
          :class="selectedGroupId === null ? 'bg-indigo-700 text-white' : 'text-gray-400 hover:text-gray-200'"
          @click="selectedGroupId = null"
        >All</button>

        <!-- Group tabs -->
        <template v-for="g in groups" :key="g.id">
          <div class="relative flex items-center">
            <button
              v-if="editingGroupId !== g.id"
              type="button"
              class="text-sm px-4 py-1.5 rounded-full transition-colors"
              :class="selectedGroupId === g.id ? 'bg-indigo-700 text-white' : 'text-gray-400 hover:text-gray-200'"
              @click="selectedGroupId = g.id"
            >{{ g.name }}</button>
            <input
              v-else
              v-model="editingGroupName"
              type="text"
              class="text-sm px-2 py-1 rounded bg-darker border border-gray-600 text-gray-200 focus:outline-none focus:border-blue-500 w-32"
              @keydown.enter="commitRenameGroup(g)"
              @keydown.esc="editingGroupId = null"
              @blur="commitRenameGroup(g)"
            />
            <template v-if="groupEditMode && editingGroupId !== g.id">
              <button type="button" class="ml-0.5 text-gray-600 hover:text-gray-400 text-sm" @click="startRenameGroup(g)">&#9998;</button>
              <button type="button" class="text-gray-600 hover:text-red-400 text-sm" @click="handleDeleteGroup(g.id)">&#215;</button>
            </template>
          </div>
        </template>

        <!-- Right side controls -->
        <div class="ml-auto flex items-center gap-1.5">
          <button
            type="button"
            class="text-sm px-3 py-1.5 rounded border border-gray-700 text-gray-500 hover:text-gray-300"
            @click="handleExportAll"
          >Export All</button>
          <button
            type="button"
            class="text-sm px-3 py-1.5 rounded border border-gray-700 text-gray-500 hover:text-gray-300"
            @click="handleImportGroup"
          >Import</button>
          <button
            type="button"
            class="text-sm px-3 py-1.5 rounded border"
            :class="groupEditMode ? 'border-indigo-500 text-indigo-400' : 'border-gray-700 text-gray-600 hover:text-gray-400'"
            @click="groupEditMode = !groupEditMode; reportEditMode = !reportEditMode"
          >{{ groupEditMode ? 'Done' : 'Edit' }}</button>
        </div>
      </div>

      <!-- Group management row -->
      <div v-if="loadedAccountId" class="flex items-center gap-1.5 px-2 pt-1.5 pb-1.5 flex-shrink-0">
        <input
          v-if="showNewGroupInput"
          v-model="newGroupName"
          type="text"
          class="text-sm px-2 py-1 rounded bg-darker border border-gray-600 text-gray-200 focus:outline-none focus:border-blue-500 w-32"
          placeholder="Group name"
          @keydown.enter="handleCreateGroup"
          @keydown.esc="showNewGroupInput = false; newGroupName = ''"
          @blur="handleCreateGroup"
        />
        <button
          v-else
          type="button"
          class="text-sm px-3 py-1.5 rounded border border-gray-700 text-gray-500 hover:text-gray-300"
          @click="showNewGroupInput = true"
        >+ New Group</button>
      </div>

      <ReportGrid v-if="loadedAccountId" :account-id="loadedAccountId" :group-filter="selectedGroupId" :edit-mode="reportEditMode" />
      <div v-else class="h-full flex items-center justify-center">
        <p class="text-xs text-gray-500">Select an account and click Load to view reports.</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import ReportGrid from '../components/ReportGrid.vue'
import SegmentedProgressBar, { type ProgressSegment } from '../components/SegmentedProgressBar.vue'
import { useAppState } from '../composables/useAppState'
import { useActiveAccount } from '../composables/useActiveAccount'
import { useReports } from '../composables/useReports'
import { useReportGroups } from '../composables/useReportGroups'
import { readImportFile } from '../utils/reportIO'
import type { ReportGroup } from '../types/bridge'

const { existingData } = useAppState()
const { activeAccountId } = useActiveAccount()
const { backfillStatus, loadReports, refreshBackfillStatus } = useReports()
const { groups, loadGroups, createGroup, renameGroup, deleteGroup } = useReportGroups()

const loadedAccountId = ref<string | null>(null)
const doesDataExist = computed(() => existingData.value.length > 0)
const selectedGroupId = ref<string | null>(null)
const groupEditMode = ref(false)
const reportEditMode = ref(false)
const newGroupName = ref('')
const editingGroupId = ref<string | null>(null)
const editingGroupName = ref('')
const showNewGroupInput = ref(false)

const backfillSegments = computed<ProgressSegment[]>(() => [{
  label: 'Drop index',
  status: backfillStatus.value.done ? 'done' : 'active',
  color: 'blue',
  widthPct: Math.round(backfillStatus.value.progress * 100),
  pulsing: !backfillStatus.value.done,
}])

async function loadAccountReports(id: string) {
  loadedAccountId.value = id
  await loadReports(id)
  await loadGroups(id)
  selectedGroupId.value = null
}

watch(activeAccountId, (id) => {
  if (id) void loadAccountReports(id)
}, { immediate: true })

async function handleCreateGroup() {
  const name = newGroupName.value.trim()
  showNewGroupInput.value = false
  newGroupName.value = ''
  if (!name || !loadedAccountId.value) return
  const id = await createGroup(loadedAccountId.value, name)
  if (id) selectedGroupId.value = id
}

function startRenameGroup(g: ReportGroup) {
  editingGroupId.value = g.id
  editingGroupName.value = g.name
}

async function commitRenameGroup(g: ReportGroup) {
  const name = editingGroupName.value.trim()
  editingGroupId.value = null
  if (!name || name === g.name || !loadedAccountId.value) return
  await renameGroup(loadedAccountId.value, g.id, name)
}

async function handleDeleteGroup(id: string) {
  if (!loadedAccountId.value) return
  await deleteGroup(loadedAccountId.value, id)
  if (selectedGroupId.value === id) selectedGroupId.value = null
}

async function handleExportAll() {
  if (!loadedAccountId.value) return
  const defaultName = `reports-export-${Math.floor(Date.now() / 1000)}.json`
  const destPath = await globalThis.chooseSaveFilePath(defaultName)
  if (!destPath) return
  const path = await globalThis.exportAllReports(loadedAccountId.value, destPath)
  if (path) globalThis.openFileInFolder(path)
}

async function handleExportGroup() {
  if (!selectedGroupId.value) return
  const json = await globalThis.exportGroupReports(selectedGroupId.value)
  if (!json) return
  const group = groups.value.find(g => g.id === selectedGroupId.value)
  const name = group?.name ?? 'group'
  const blob = new Blob([json], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = name.replaceAll(/[^a-z0-9-_]/gi, '_').toLowerCase() + '-reports.json'
  a.click()
  URL.revokeObjectURL(url)
}

async function handleImportGroup() {
  if (!loadedAccountId.value) return
  const json = await readImportFile()
  if (!json) return
  const id = await globalThis.importGroupReports(loadedAccountId.value, json)
  if (id) {
    await loadGroups(loadedAccountId.value)
    await loadReports(loadedAccountId.value)
    selectedGroupId.value = id
  }
}

let pollTimer: ReturnType<typeof setInterval> | null = null

onMounted(async () => {
  await refreshBackfillStatus()
  if (!backfillStatus.value.done) {
    pollTimer = setInterval(async () => {
      await refreshBackfillStatus()
      if (backfillStatus.value.done && pollTimer) {
        clearInterval(pollTimer)
        pollTimer = null
      }
    }, 2000)
  }
})

onUnmounted(() => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
})
</script>
