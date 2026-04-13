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
    <div>
      <form
        id="ledgerForm"
        name="ledgerForm"
        class="select-form"
        @submit="onSubmit"
      >
        <div ref="playerIdSelectRef" class="tooltip-custom relative flex-grow focus-within:z-10">
          <div v-if="selectedAccount?.nickname || (screenshotSafety && playerId)" class="ledger-input-overlay">
            <span class="whitespace-pre">{{ maskEid(playerId) }}</span>
            <template v-if="selectedAccount?.nickname">
              (<span :style="'color: #' + (selectedAccount?.accountColor ?? '')">
                {{ selectedAccount?.nickname }} {{ selectedAccount?.ebString ?? '???' }}
              </span>
              <template v-if="selectedAccount?.seString">
                <span class="text-gray-400">&nbsp;·</span>
                <img :src="'images/soul_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
                <span style="color:#a855f7">{{ selectedAccount?.seString }} SE</span>
                <span class="text-gray-400"> ·</span>
                <img :src="'images/prophecy_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
                <span style="color:#eab308">{{ selectedAccount?.peCount }} PE</span>
                <template v-if="selectedAccount?.teCount">
                  <span class="text-gray-400"> ·</span>
                  <img :src="'images/truth_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
                  <span style="color:#c831ff">{{ selectedAccount?.teCount }} TE</span>
                </template>
              </template>)
            </template>
          </div>
          <div v-if="!isPlayerIdValid" class="ledger-input-overlay">
            <span class="whitespace-pre">
              <template v-if="!screenshotSafety">{{ playerId }} </template>(<span class="text-red-700">Invalid EID: {{ getEidProblem(playerId) }}</span>)
            </span>
          </div>
          <input
            id="playerIdInput"
            type="text"
            :class="'drop-select ' + (isPlayerIdValid ? 'border-gray-300' : 'border-red-700')"
            :style="(selectedAccount?.nickname || (screenshotSafety && playerId)) ? { color: 'transparent', caretColor: '#9ca3af' } : undefined"
            placeholder="EI1234567890123456"
            :value="playerId"
            @focus="openPlayerIdDropdown"
            @input="(e) => (playerId = (e.target as HTMLInputElement).value)"
          />
          <ul
            v-if="playerIdDropdownOpen && (knownAccounts?.length ?? 0) > 0"
            class="ledger-list"
            tabindex="-1"
          >
            <li
              v-for="account in knownAccounts"
              :key="account.id"
              class="drop-opt"
              @click="closePlayerIdDropdown(account.id)"
            >
              {{ maskEid(account.id) }}
              (<span :style="'color: #' + (account.accountColor ?? '')">
                {{ account.nickname }}
                {{ account.ebString ?? '???' }}
              </span>
              <template v-if="account.seString">
                <span class="text-gray-400">&nbsp;·</span>
                <img :src="'images/soul_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
                <span style="color:#a855f7">{{ account.seString }} SE</span>
                <span class="text-gray-400"> ·</span>
                <img :src="'images/prophecy_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
                <span style="color:#eab308">{{ account.peCount }} PE</span>
                <template v-if="account.teCount">
                  <span class="text-gray-400"> ·</span>
                  <img :src="'images/truth_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
                  <span style="color:#c831ff">{{ account.teCount }} TE</span>
                </template>
              </template>)
            </li>
          </ul>
        </div>
        <button
          v-if="idle"
          type="submit"
          class="fetch-button disabled:hover:darker_tab_hover"
          :disabled="playerId.trim() === '' || !isPlayerIdValid"
        >
          Fetch
        </button>
        <button
          v-else
          type="button"
          class="fetch-button disabled:hover:darker_tab_hover"
          @click="onStop"
        >
          Stop
        </button>
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
        <div class="text-yellow-400">Fetching missions...</div>
        <div>
          <span class="text-green-400">{{ progress?.finished ?? 0 }}</span>
          <span class="text-gray-400"> / {{ progress?.total ?? 0 }}  ETA {{ etaStr }}</span>
        </div>
        <div v-if="(progress?.failed ?? 0) > 0 || (progress?.retried ?? 0) > 0">
          <span v-if="(progress?.failed ?? 0) > 0" class="text-red-500">{{ progress?.failed }} failed</span>
          <span v-if="(progress?.failed ?? 0) > 0 && (progress?.retried ?? 0) > 0" class="text-gray-400"> · </span>
          <span v-if="(progress?.retried ?? 0) > 0" class="text-yellow-400">{{ progress?.retried }} retried</span>
        </div>
        <div v-if="progress?.currentMission" class="text-gray-400 italic">{{ progress.currentMission }}</div>
      </template>
      <template v-else-if="appState === AppState.ExportingData">Exporting data...</template>
      <template v-else-if="appState === AppState.Success">
        Successfully exported to:
        <div class="grid gap-x-2" style="grid-template-columns: repeat(2, max-content)">
          <template v-for="file in exportedFiles" :key="file">
            <button class="file-link" @click="openFile(file)">{{ file }}</button>
            <button class="url-link truncate" @click="openFileInFolder(file)">open in folder</button>
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
                segmentStates.seg2 === 'skipped' ? 'bg-gray-600' :
                segmentStates.seg2 === 'active' || segmentStates.seg2 === 'done' ? 'bg-green-500' :
                'bg-transparent',
                segmentStates.missionPulsing ? 'animate-pulse' : '',
              ]"
              :style="{
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
          <div class="flex-1 text-center">Save</div>
          <div class="flex-1 text-center">Missions</div>
          <div class="flex-1 text-center">Export</div>
        </div>
      </template>

    </div>

    <MissionProgressPanel :processes="missionProcesses" />

    <div
      ref="messagesRef"
      class="flex-1 min-h-0 px-2 py-1 overflow-auto shadow-sm block text-xs font-mono text-gray-400 bg-darkest rounded-md"
    >
      <div v-for="(message, i) in logMessages" :key="i" class="whitespace-pre">
        <span :class="message.isError ? 'text-red-700' : 'text-green-700'">{{ hhmmss(new Date()) }}|</span>
        <template v-for="(segment, j) in parseLogSegments(maskEid(message.message))" :key="j">
          <img
            v-if="segment.type === 'image'"
            :src="segment.src"
            style="display: inline; height: 1em; vertical-align: middle"
            alt=""
          />
          <span
            v-else-if="segment.type === 'text' && segment.color"
            :style="'color: ' + segment.color"
          >{{ segment.text }}</span>
          <span
            v-else-if="segment.type === 'text'"
            :class="message.isError ? 'text-red-700' : ''"
          >{{ segment.text }}</span>
        </template>
      </div>
    </div>
  </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useAppState } from '../composables/useAppState'
import { useFetch } from '../composables/useFetch'
import { parseLogSegments } from '../composables/useLogRenderer'
import { maskEid, screenshotSafety } from '../composables/useSettings'
import { useDropdownSelector } from '../composables/useDropdownSelector'
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

const { progress, fetchPlayerData, stopFetching } = useFetch()

const playerId = ref<string>(knownAccounts.value?.[0]?.id ?? '')
const messagesRef = ref<HTMLElement | null>(null)

const missionProcesses = computed(() =>
  processLogs.value.filter((p) => p.kind === 'mission'),
)

const {
  containerRef: playerIdSelectRef,
  isOpen: playerIdDropdownOpen,
  open: openPlayerIdDropdown,
  close: closePlayerIdDropdown,
} = useDropdownSelector((id) => { playerId.value = id })

function normalizePlayerId(id: string): string {
  id = id.trim()
  if (/^EI\d{16}$/i.test(id)) return id.toUpperCase()
  return id
}

const isPlayerIdValid = computed(() => /(^$)|(^EI\d{16}$)/.test(normalizePlayerId(playerId.value)))
const selectedAccount = computed(
  () => knownAccounts.value?.find((acc) => acc.id === normalizePlayerId(playerId.value)) ?? null,
)

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


function getEidProblem(id: string): string {
  const normalizedId = normalizePlayerId(id).toUpperCase()
  if (normalizedId.substring(0, 2) !== 'EI') return 'should start with "EI"'
  if (!/^EI\d*$/.test(normalizedId) || normalizedId.length === 2) return 'should be EI + 16 digits'
  if (normalizedId.length !== 18) return 'expected 16 digits, found ' + (normalizedId.length - 2)
  return ''
}

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
  closePlayerIdDropdown()
  const normalized = normalizePlayerId(playerId.value)
  if (normalized === '') return
  await fetchPlayerData(normalized)
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
