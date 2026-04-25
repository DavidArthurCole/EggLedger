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

    <!-- Account selector -->
    <DatabaseAccountSelector
      v-if="doesDataExist"
      :accounts="existingData"
      button-label="Load"
      style="margin-top: 0rem !important"
      @submit="onViewSubmit"
    />

    <div v-if="!doesDataExist" class="flex-1 flex items-center justify-center">
      <p class="text-xs text-gray-500">No mission data found. Fetch data on the Ledger tab first.</p>
    </div>

    <div v-else class="flex-1 min-h-0 bg-darkest rounded-md overflow-hidden flex flex-col">
      <ReportGrid v-if="loadedAccountId" :account-id="loadedAccountId" />
      <div v-else class="h-full flex items-center justify-center">
        <p class="text-xs text-gray-500">Select an account and click Load to view reports.</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import ReportGrid from '../components/ReportGrid.vue'
import DatabaseAccountSelector from '../components/DatabaseAccountSelector.vue'
import SegmentedProgressBar, { type ProgressSegment } from '../components/SegmentedProgressBar.vue'
import { useAppState } from '../composables/useAppState'
import { useReports } from '../composables/useReports'

const { existingData } = useAppState()
const { backfillStatus, loadReports, refreshBackfillStatus } = useReports()

const loadedAccountId = ref<string | null>(null)
const doesDataExist = computed(() => existingData.value.length > 0)

const backfillSegments = computed<ProgressSegment[]>(() => [{
  label: 'Drop index',
  status: backfillStatus.value.done ? 'done' : 'active',
  color: 'blue',
  widthPct: Math.round(backfillStatus.value.progress * 100),
  pulsing: !backfillStatus.value.done,
}])

async function onViewSubmit(id: string) {
  loadedAccountId.value = id
  await loadReports(id)
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
