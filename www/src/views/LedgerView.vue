<template>
  <div class="flex-1 min-h-0 flex flex-col overflow-hidden">
    <!-- Forbidden/translocated warnings -->
    <TranslocationModal :visible="appIsTranslocated" />
    <ForbiddenDirModal :visible="appIsInForbiddenDirectory && !appIsTranslocated" />

    <!-- Main ledger UI -->
    <div
      v-if="!appIsInForbiddenDirectory && !appIsTranslocated"
      class="view-layout overflow-hidden"
    >
    <div class="flex flex-col items-center gap-2 py-2">
      <div class="text-xs text-gray-400 bg-darker border border-gray-700 rounded-lg px-4 py-2 text-center min-h-4">
        <template v-if="activeAccountId && activeAccountInfo">
          <span :style="'color: #' + activeAccountInfo.accountColor" class="font-medium">{{ activeAccountInfo.nickname }}</span>
          <span class="text-gray-500 ml-1.5">{{ activeAccountInfo.ebString }}</span>
          <template v-if="activeAccountInfo.seString">
            <span class="text-gray-400 ml-1.5">·</span>
            <img :src="'images/soul_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
            <span style="color:#a855f7">{{ activeAccountInfo.seString }} SE</span>
            <span class="text-gray-400 ml-1.5">·</span>
            <img :src="'images/prophecy_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
            <span style="color:#eab308">{{ activeAccountInfo.peCount }} PE</span>
            <template v-if="activeAccountInfo.teCount">
              <span class="text-gray-400 ml-1.5">·</span>
              <img :src="'images/truth_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
              <span style="color:#c831ff">{{ activeAccountInfo.teCount }} TE</span>
            </template>
          </template>
        </template>
        <span v-else class="text-gray-500 italic">Select an account from the top right to get started</span>
      </div>
      <form id="ledgerForm" name="ledgerForm" @submit="onSubmit">
        <button
          v-if="idle"
          type="submit"
          class="fetch-button disabled:opacity-50 disabled:hover:darker_tab_hover"
          :disabled="!activeAccountId"
        >Fetch</button>
        <button
          v-else
          type="button"
          class="fetch-button"
          @click="onStop"
        >Stop</button>
      </form>
    </div>

    <div class="min-h-14 px-2 py-1 text-xs text-gray-500 bg-darkest rounded-md tabular-nums">
      <template v-if="appState === AppState.FetchingSave">Fetching save...</template>
      <template v-else-if="appState === AppState.ResolvingMissionTypes">
        <div class="text-yellow-400">Resolving mission types...</div>
        <div>
          <span class="text-green-400">{{ progress?.finished ?? 0 }}</span>
          <span class="text-gray-400"> / {{ progress?.total ?? 0 }}</span>
        </div>
      </template>
      <template v-else-if="appState === AppState.FetchingMissions">
        <div class="text-yellow-400">Fetching missions... <span v-if="progress?.currentMission" class="text-gray-400 italic">{{ progress.currentMission }}</span></div>
        <div>
          <span class="text-green-400">{{ progress?.finished ?? 0 }}</span>
          <span class="text-gray-400"> / {{ progress?.total ?? 0 }}  ETA {{ etaStr }}</span>
        </div>
        <div v-if="(progress?.failed ?? 0) > 0 || (progress?.retried ?? 0) > 0">
          <span v-if="(progress?.failed ?? 0) > 0" class="text-red-500">{{ progress?.failed }} failed</span>
          <span v-if="(progress?.failed ?? 0) > 0 && (progress?.retried ?? 0) > 0" class="text-gray-400"> · </span>
          <span v-if="(progress?.retried ?? 0) > 0" class="text-orange-400">{{ progress?.retried }} to retry</span>
        </div>
      </template>
      <template v-else-if="appState === AppState.ExportingData">Exporting data...</template>
      <template v-else-if="appState === AppState.Success">
        Successfully exported to:
        <div class="grid gap-x-2" style="grid-template-columns: repeat(2, max-content)">
          <template v-for="file in exportedFiles" :key="file">
            <button class="file-link p-0" @click="openFile(file)"><template v-if="screenshotSafety"><template v-for="(part, partIdx) in splitMaskedPath(file)" :key="partIdx"><span v-if="part.mask" class="inline-block rounded-sm bg-current select-none" style="width: 16ch; height: 0.8em; vertical-align: -0.05em;"></span><template v-else>{{ part.text }}</template></template></template><template v-else>{{ file }}</template></button>
            <button class="url-link truncate p-0" @click="openFileInFolder(file)">open in folder</button>
          </template>
        </div>
      </template>
      <template v-else-if="appState === AppState.Failed">
        Data fetching failed. Please try again.<br />
      </template>
      <template v-else-if="appState === AppState.Interrupted">Interrupted.</template>
      <template v-if="showSegmentedBar">
        <div class="flex gap-1 mt-1">
          <!-- Segment 1: Fetch Save (blue) -->
          <div class="flex-1 h-3 bg-dark rounded-sm overflow-hidden">
            <div
              class="h-full transition-all duration-300 rounded-sm"
              :class="[
                segmentStates.seg1 === 'active' ? 'bg-blue-500 animate-pulse' :
                segmentStates.seg1 === 'done' ? 'bg-blue-500' :
                'bg-transparent',
              ]"
              :style="{ width: segmentStates.seg1 === 'pending' ? '0%' : '100%' }"
            ></div>
          </div>
          <!-- Segment 2: Fetch Missions (green) -->
          <div class="flex-1 h-3 bg-dark rounded-sm overflow-hidden">
            <div
              class="h-full transition-all duration-300 rounded-sm"
              :class="[
                segmentStates.seg2 === 'active' || segmentStates.seg2 === 'done' ? 'bg-green-500' :
                segmentStates.seg2 === 'skipped' ? '' :
                'bg-transparent',
                segmentStates.missionPulsing ? 'animate-pulse' : '',
              ]"
              :style="segmentStates.seg2 === 'skipped'
                ? { width: '100%', backgroundImage: 'repeating-linear-gradient(45deg, transparent, transparent 3px, rgba(16,185,129,0.3) 3px, rgba(16,185,129,0.3) 5px)' }
                : {
                  width:
                    segmentStates.seg2 === 'pending' ? '0%' :
                    segmentStates.seg2 === 'active' ? `${segmentStates.missionPct}%` :
                    '100%',
                }"
            ></div>
          </div>
          <!-- Segment 3: Export (amber) -->
          <div class="flex-1 h-3 bg-dark rounded-sm overflow-hidden">
            <div
              class="h-full transition-all duration-300 rounded-sm"
              :class="[
                segmentStates.seg3 === 'active' ? 'bg-amber-500 animate-pulse' :
                segmentStates.seg3 === 'done' ? 'bg-amber-500' :
                'bg-transparent',
              ]"
              :style="{ width: segmentStates.seg3 === 'pending' ? '0%' : '100%' }"
            ></div>
          </div>
        </div>
        <div class="flex mt-0.5 text-gray-600" style="font-size: 0.6rem">
          <div
            class="flex-1 text-center"
            :class="segmentStates.seg1 === 'active' ? 'animate-pulse' : ''"
            :style="segmentStates.seg1 !== 'pending' ? 'color: rgb(59 130 246)' : ''"
          >Save</div>
          <div
            class="flex-1 text-center"
            :class="segmentStates.missionPulsing ? 'animate-pulse' : ''"
            :style="(segmentStates.seg2 === 'active' || segmentStates.seg2 === 'done') ? 'color: rgb(16 185 129)' : ''"
          >Missions</div>
          <div
            class="flex-1 text-center"
            :class="segmentStates.seg3 === 'active' ? 'animate-pulse' : ''"
            :style="segmentStates.seg3 !== 'pending' ? 'color: rgb(245 158 11)' : ''"
          >Export</div>
        </div>
      </template>

    </div>

    <MissionProgressPanel v-if="showMissionProgress" :processes="missionProcesses" />

    <div
      ref="messagesRef"
      class="flex-1 min-h-0 px-2 py-1 overflow-auto shadow-sm block text-xs font-mono text-gray-400 bg-darkest rounded-md"
    >
      <div v-for="(message, i) in logMessages" :key="i" class="whitespace-pre">
        <span :class="message.isError ? 'text-red-700' : 'text-green-700'">{{ hhmmss(new Date(message.timestamp)) }}|</span>
        <LogLine :text="message.message" :is-error="message.isError" />
      </div>
    </div>
  </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useAppState } from '../composables/useAppState'
import { useFetch } from '../composables/useFetch'
import { screenshotSafety, showMissionProgress } from '../composables/useSettings'
import { useActiveAccount } from '../composables/useActiveAccount'
import LogLine from '../components/LogLine.vue'
import { AppState } from '../types/bridge'
import ForbiddenDirModal from '../components/modals/ForbiddenDirModal.vue'
import TranslocationModal from '../components/modals/TranslocationModal.vue'
import MissionProgressPanel from '../components/MissionProgressPanel.vue'

const {
  appIsInForbiddenDirectory,
  appIsTranslocated,
  knownAccounts,
  existingData,
  appState,
  logMessages,
  exportedFiles,
  processLogs,
} = useAppState()

const { activeAccountId } = useActiveAccount()

const { progress, fetchPlayerData, stopFetching } = useFetch()

const messagesRef = ref<HTMLElement | null>(null)

const missionProcesses = computed(() =>
  processLogs.value.filter((p) => p.kind === 'mission'),
)

const activeAccountInfo = computed(
  () => knownAccounts.value?.find((acc) => acc.id === activeAccountId.value) ?? null,
)

function splitMaskedPath(path: string): Array<{ text?: string; mask?: boolean }> {
  const parts: Array<{ text?: string; mask?: boolean }> = []
  const regex = /EI(\d{16})/g
  let lastIndex = 0
  let match: RegExpExecArray | null
  while ((match = regex.exec(path)) !== null) {
    if (match.index > lastIndex) parts.push({ text: path.slice(lastIndex, match.index) })
    parts.push({ text: 'EI' }, { mask: true })
    lastIndex = match.index + match[0].length
  }
  if (lastIndex < path.length) parts.push({ text: path.slice(lastIndex) })
  return parts
}

const idle = computed(() => {
  const s = appState.value
  return (
    s !== AppState.FetchingSave &&
    s !== AppState.ResolvingMissionTypes &&
    s !== AppState.FetchingMissions &&
    s !== AppState.ExportingData
  )
})

const hadMissions = ref(false)
const exportEntered = ref(false)

watch(appState, (s) => {
  if (s === AppState.FetchingSave) {
    hadMissions.value = false
    exportEntered.value = false
  }
  if (s === AppState.ResolvingMissionTypes || s === AppState.FetchingMissions) hadMissions.value = true
  if (s === AppState.ExportingData) exportEntered.value = true
})

type SegmentStatus = 'pending' | 'active' | 'done' | 'skipped'

const showSegmentedBar = computed(
  () => appState.value !== '' && appState.value !== AppState.AwaitingInput,
)

const segmentStates = computed(() => {
  const s = appState.value
  const p = progress.value

  const afterSave = (
    s === AppState.ResolvingMissionTypes ||
    s === AppState.FetchingMissions ||
    s === AppState.ExportingData ||
    s === AppState.Success ||
    s === AppState.Failed ||
    s === AppState.Interrupted
  )

  const afterMissions = (
    s === AppState.ExportingData ||
    s === AppState.Success ||
    s === AppState.Failed ||
    s === AppState.Interrupted
  )

  const terminal = (
    s === AppState.Success ||
    s === AppState.Failed ||
    s === AppState.Interrupted
  )

  let seg1: SegmentStatus = 'pending'
  if (s === AppState.FetchingSave) seg1 = 'active'
  else if (afterSave) seg1 = 'done'

  let seg2: SegmentStatus = 'pending'
  if (s === AppState.ResolvingMissionTypes || s === AppState.FetchingMissions) seg2 = 'active'
  else if (afterMissions && hadMissions.value) seg2 = 'done'
  else if (afterMissions && !hadMissions.value) seg2 = 'skipped'

  let seg3: SegmentStatus = 'pending'
  if (s === AppState.ExportingData) seg3 = 'active'
  else if (exportEntered.value && terminal) seg3 = 'done'

  let missionPct = 0
  if (s === AppState.FetchingMissions && p?.total) {
    missionPct = Math.round((p.finished / p.total) * 100)
  } else if (seg2 === 'done') {
    missionPct = 100
  }

  // When the Missions segment is active but no individual-mission progress has
  // registered yet (ResolvingMissionTypes, or FetchingMissions just started),
  // pulse the bar at a minimum visible width so the user can see the stage is
  // active rather than looking identical to the Save-done state.
  const missionPulsing = seg2 === 'active' && missionPct === 0
  const visibleMissionPct = seg2 === 'active' ? Math.max(missionPct, 3) : missionPct

  return { seg1, seg2, seg3, missionPct: visibleMissionPct, missionPulsing }
})


function hhmmss(date: Date): string {
  return `${date.getHours().toString().padStart(2, '0')}:${date
    .getMinutes()
    .toString()
    .padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
}


function getEta(finish: number): string {
  const eta = Math.round(Math.max(finish - Date.now() / 1000, 0))
  const h = Math.floor(eta / 3600).toString()
  const mm = Math.floor((eta % 3600) / 60).toString().padStart(2, '0')
  const ss = Math.floor(eta % 60).toString().padStart(2, '0')
  return `${h}:${mm}:${ss}`
}

const etaStr = ref('')
let etaIntervalId: ReturnType<typeof setInterval> | undefined

onMounted(() => {
  etaIntervalId = setInterval(() => {
    if (progress.value) etaStr.value = getEta(progress.value.expectedFinishTimestamp)
  }, 200)
})
onUnmounted(() => {
  clearInterval(etaIntervalId)
})

async function onSubmit(event: Event) {
  event.preventDefault()
  if (!activeAccountId.value) return
  await fetchPlayerData(activeAccountId.value)
  existingData.value = await globalThis.getExistingData()
}

async function onStop() {
  await stopFetching()
  existingData.value = await globalThis.getExistingData()
}

async function openFile(file: string) {
  await globalThis.openFile(file)
}
async function openFileInFolder(file: string) {
  await globalThis.openFileInFolder(file)
}

// Auto scroll messages
watch(
  logMessages,
  () => {
    const el = messagesRef.value
    if (el) {
      el.scrollTop = el.scrollHeight
    }
  },
  { deep: true, flush: 'post' },
)


</script>
