<template>
  <div class="flex-1 flex flex-col overflow-hidden">
    <!-- Forbidden/translocated warnings -->
    <TranslocationModal :visible="appIsTranslocated" />
    <ForbiddenDirModal :visible="appIsInForbiddenDirectory && !appIsTranslocated" />

    <!-- Main ledger UI -->
    <div
      v-if="!appIsInForbiddenDirectory && !appIsTranslocated"
      class="flex-1 flex flex-col w-full mx-auto px-4 space-y-3 overflow-hidden bg-darker"
    >
    <div>
      <form
        id="ledgerForm"
        name="ledgerForm"
        class="select-form"
        @submit="onSubmit"
      >
        <div ref="playerIdSelectRef" class="tooltip-custom relative flex-grow focus-within:z-10">
          <div v-if="nicknameForSelectedPlayerId" class="ledger-input-overlay">
            <span class="whitespace-pre">{{ playerId }}</span>
            (<span :style="'color: #' + (objectedExistingData.find(acc => acc.id === playerId)?.accountColor || '')">
              {{ nicknameForSelectedPlayerId }} {{ objectedExistingData.find(acc => acc.id === playerId)?.ebString ?? '???' }}
            </span>)
          </div>
          <div v-if="!isPlayerIdValid" class="ledger-input-overlay">
            <span class="whitespace-pre">
              {{ playerId }} ( <span class="text-red-700">Invalid EID: {{ getEidProblem(playerId) }}</span> )
            </span>
          </div>
          <input
            id="playerIdInput"
            type="text"
            :class="'drop-select ' + (isPlayerIdValid ? 'border-gray-300' : 'border-red-700')"
            placeholder="EI1234567890123456"
            :value="playerId"
            @focus="openPlayerIdDropdown"
            @input="(e) => (playerId = (e.target as HTMLInputElement).value)"
          />
          <ul
            v-if="playerIdDropdownOpen && knownAccounts.length > 0"
            class="ledger-list focus:outline-none sm:text-sm"
            tabindex="-1"
          >
            <li
              v-for="account in knownAccounts"
              :key="account.id"
              class="drop-opt"
              @click="closePlayerIdDropdown(account.id)"
            >
              {{ account.id }}
              (<span :style="'color: #' + (objectedExistingData.find(acc => acc.id === account.id)?.accountColor || '')">
                {{ objectedExistingData.find(acc => acc.id === account.id)?.nickname }}
                {{ objectedExistingData.find(acc => acc.id === account.id)?.ebString ?? '???' }}
              </span>)
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
        <div class="h-3 relative rounded-full overflow-hidden mt-1">
          <div class="w-full h-full bg-dark absolute"></div>
          <div
            class="h-full absolute rounded-full bg-green-500"
            :style="{ width: progress?.finishedPercentage ?? '0%' }"
          ></div>
        </div>
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
    </div>

    <div
      ref="messagesRef"
      class="flex-1 px-2 py-1 overflow-auto shadow-sm block text-xs font-mono text-gray-400 bg-darkest rounded-md"
    >
      <div v-for="(message, i) in logMessages" :key="i" class="whitespace-pre">
        <span :class="message.isError ? 'text-red-700' : 'text-green-700'">{{ hhmmss(new Date()) }}|</span>
        <template v-for="(segment, j) in parseLogSegments(message.message)" :key="j">
          <img
            v-if="segment.type === 'image'"
            :src="segment.src"
            style="height: 1em; vertical-align: middle"
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
import { AppState } from '../types/bridge'
import ForbiddenDirModal from '../components/modals/ForbiddenDirModal.vue'
import TranslocationModal from '../components/modals/TranslocationModal.vue'

const {
  appIsInForbiddenDirectory,
  appIsTranslocated,
  knownAccounts,
  existingData,
  appState,
  logMessages,
  exportedFiles,
} = useAppState()

const { progress, fetchPlayerData, stopFetching } = useFetch()

const playerId = ref<string>(knownAccounts.value[0]?.id ?? '')
const playerIdSelectRef = ref<HTMLElement | null>(null)
const playerIdDropdownOpen = ref(false)
const messagesRef = ref<HTMLElement | null>(null)

function normalizePlayerId(id: string): string {
  id = id.trim()
  if (/^EI\d{16}$/i.test(id)) return id.toUpperCase()
  return id
}

const isPlayerIdValid = computed(() => /(^$)|(^EI\d{16}$)/.test(normalizePlayerId(playerId.value)))
const nicknameForSelectedPlayerId = computed(
  () => knownAccounts.value.find((acc) => acc.id === normalizePlayerId(playerId.value))?.nickname ?? '',
)

const objectedExistingData = computed(() => {
  return existingData.value
    .map((account) => ({
      id: account.id,
      nickname: account.nickname,
      missionCount: account.missionCount,
      ebString: account.ebString && account.ebString !== '' ? account.ebString : '???',
      accountColor: account.accountColor,
    }))
    .sort((a, b) => {
      if (a.missionCount > b.missionCount) return -1
      if (a.missionCount < b.missionCount) return 1
      return 0
    })
})

const idle = computed(() => {
  const s = appState.value
  return s !== AppState.FetchingSave && s !== AppState.FetchingMissions && s !== AppState.ExportingData
})

function openPlayerIdDropdown() {
  playerIdDropdownOpen.value = true
}
function closePlayerIdDropdown(id?: string) {
  if (id != null && id !== '') playerId.value = id
  playerIdDropdownOpen.value = false
}

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
onUnmounted(() => clearInterval(etaIntervalId))

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
