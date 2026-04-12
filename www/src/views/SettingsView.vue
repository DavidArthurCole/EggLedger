<template>
  <div class="view-layout overflow-y-scroll">
    <div class="flex-1 px-3 py-2 overflow-auto shadow-sm block text-sm font-mono text-gray-400 bg-darkest rounded-md">
      <div class="w-full pl-0_5rem pr-0_5rem">
        <span class="text-base">
          Preferred browser for Ledger (<span class="text-gray-300 text-sm">effective on restart</span>)
        </span> <br />
        <div ref="preferredBrowserSelectRef" class="text-sm relative w-full flex-grow focus-within:z-10 pl-0_5rem">
          <div v-if="preferredBrowser" class="ledger-input-overlay">
            <span>{{ preferredBrowser }}</span> (<img v-if="getBrowserIcon(preferredBrowser)" :src="getBrowserIcon(preferredBrowser)" :alt="getBrowserDisplayName(preferredBrowser)" class="inline-block w-4 h-4 align-text-bottom mr-1" /><span class="text-gray-300">{{ getBrowserDisplayName(preferredBrowser) }}</span>)
          </div>
          <input
            id="preferredBrowserInput"
            type="text"
            class="drop-select-full text-sm bg-darker"
            placeholder="-- Not Set (Default) --"
            :value="preferredBrowser"
            @focus="openPrefBrowserDropdown"
            @input="(e) => (e as Event).preventDefault()"
          />
          <ul
            v-if="prefBrowserDropdownOpen && allBrowsers.length > 0"
            class="ledger-list"
            tabindex="-1"
          >
            <li
              v-for="browser in allBrowsers"
              :key="browser"
              class="drop-opt bg-darker"
              @click="closePrefBrowserDropdown(browser)"
            >
              {{ browser }} <span class="inline-block max-w-9/10">(<img v-if="getBrowserIcon(browser)" :src="getBrowserIcon(browser)" :alt="getBrowserDisplayName(browser)" class="inline-block w-4 h-4 align-text-bottom mr-1" /><span class="text-gray-300">{{ getBrowserDisplayName(browser) }}</span>)</span>
            </li>
          </ul>
        </div>
      </div>

      <hr class="mt-1rem mb-1rem w-full" />

      <div class="text-sm relative w-full pl-0_5rem pr-0_5rem">
        <span class="text-base">
          App resolution defaults (<span class="text-gray-300 text-sm">effective on restart</span>)
        </span> <br />
        <div class="flex flex-row items-center pl-0_5rem">
          <div class="flex flex-col w-full max-w-10rem text-center mt-0_5rem">
            <input type="number" id="resPrefX" class="number-input text-sm bg-darker" v-model="resolutionX" />
            <span class="text-sm text-gray-400 mt-0_5rem">Width<br />(<span class="text-gray-300">X</span>)</span>
          </div>
          <span class="text-xl font-bold ml-0_5rem mr-0_5rem mb-2_5rem">x</span>
          <div class="flex flex-col w-full max-w-10rem text-center mt-0_5rem">
            <input type="number" id="resPrefY" class="number-input text-sm bg-darker" v-model="resolutionY" />
            <span class="text-sm text-gray-400 mt-0_5rem">Height<br />(<span class="text-gray-300">Y</span>)</span>
          </div>
          <span class="text-xl font-bold ml-0_5rem mr-0_5rem mb-2_5rem">@</span>
          <div class="flex flex-col w-full max-w-5rem text-center mt-0_5rem">
            <input type="number" id="scalePref" class="number-input text-sm bg-darker" v-model="scalingFactor" />
            <span class="text-sm text-gray-400 mt-0_5rem">Scaling Factor</span>
          </div>
        </div>
        <div class="mt-0_5rem pl-0_5rem">
          <input
            id="startInFullscreenCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="startInFullscreen"
          />
          <label for="startInFullscreenCheckbox" class="ext-opt-label">Launch app in fullscreen</label>
        </div>
      </div>

      <hr class="mt-1rem mb-1rem w-full" />

      <div class="px-2 py-2 text-sm text-gray-400 bg-darker rounded-md tabular-nums overflow-auto">
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Menno's Ship Data</span><br />
        <span class="italic text-gray-400">Data last refreshed on
          <span :class="secondsSinceLastUpdate >= 2147483647 ? 'text-red-700' : 'text-green-500'">{{ lastUpdateString }}</span>
        </span><br />
        <button
          class="apply-filter-button mb-0_5rem"
          :disabled="secondsSinceLastUpdate < 86400"
          @click="onManualRefresh"
        >
          Refresh Data Now
        </button><br />
        <span class="italic text-sm text-gray-500" v-if="secondsSinceLastUpdate < 86400">
          To reduce the load on Menno's API, manual refreshes can be performed once every day
        </span>
        <div class="mt-0_5rem">
          <input
            id="autoRefreshMennoCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="autoRefreshMenno"
          />
          <label for="autoRefreshMennoCheckbox" class="ext-opt-label">Automatically refresh Menno data once weekly</label>
        </div>
      </div>

      <hr class="mt-1rem mb-1rem w-full" />

      <div class="px-2 py-2 text-sm text-gray-400 bg-darker rounded-md tabular-nums overflow-auto">
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Other Globals</span><br />
        <div>
          <span class="section-heading">Default Drop-Sort Method</span><br />
          <span class="opt-span">
            <label for="dvmDefault" class="ext-opt-label">Default</label>
            <input id="dvmDefault" type="radio" v-model="defaultViewMode" value="default" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="dvmIv" class="ext-opt-label">Inventory Visualizer</label>
            <input id="dvmIv" type="radio" v-model="defaultViewMode" value="iv" class="ext-opt-check" />
          </span>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">Auto-Retry Failed Missions</span><br />
          <input
            id="autoRetryCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="autoRetry"
          />
          <label for="autoRetryCheckbox" class="ext-opt-label">Automatically retry pulling missions that fail while fetching</label>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">Timeout Errors</span><br />
          <input
            id="hideTimeoutErrorsCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="hideTimeoutErrors"
          />
          <label for="hideTimeoutErrorsCheckbox" class="ext-opt-label">Hide per-mission timeout errors in the fetch log</label>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">Screenshot Safety</span><br />
          <input
            id="screenshotSafetyCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="screenshotSafety"
          />
          <label for="screenshotSafetyCheckbox" class="ext-opt-label">Mask player IDs (EIDs) wherever they appear on screen</label>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">Parallel Download Workers</span><br />
          <div class="flex items-center mt-0_5rem pl-0_5rem gap-2">
            <input
              id="workerCountInput"
              type="number"
              class="number-input text-sm bg-darker"
              :min="1"
              :max="10"
              v-model="workerCount"
            />
            <span class="text-gray-400">workers (1–10)</span>
          </div>
          <div
            v-if="!workerCountWarningRead && !hideWorkerWarning"
            class="mt-0_5rem pl-0_5rem text-red-700 border border-red-700 rounded-md py-2 px-3"
          >
            <span class="font-bold ledger-underline">Warning:</span><br />
            Higher values fetch missions faster but may trigger API rate limiting.
            Sustained use of higher values can lead to IP blocking and potential further consequences.
            <br /><br />
            <button
              type="button"
              class="btn-link dismiss-btn"
              @click="dismissWorkerCountWarning"
            >I understand</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useSettings } from '../composables/useSettings'
import { useMennoData } from '../composables/useMennoData'
import { useDropdownSelector } from '../composables/useDropdownSelector'

const {
  resolutionX,
  resolutionY,
  scalingFactor,
  startInFullscreen,
  preferredBrowser,
  allBrowsers,
  autoRefreshMenno,
  autoRetry,
  hideTimeoutErrors,
  defaultViewMode,
  workerCount,
  screenshotSafety,
  loadSettings,
  setPreferredBrowser,
  refreshBrowserList,
} = useSettings()

const { secondsSinceLastUpdate, lastUpdateString, refresh, checkRefreshNeeded } = useMennoData()

const {
  containerRef: preferredBrowserSelectRef,
  isOpen: prefBrowserDropdownOpen,
  open: openPrefBrowserDropdown,
  close: closePrefBrowserDropdown,
} = useDropdownSelector((pref) => { setPreferredBrowser(pref) })

const workerCountWarningRead = ref(false)
const hideWorkerWarning = ref(false)

async function dismissWorkerCountWarning() {
  await globalThis.setWorkerCountWarningRead(true)
  workerCountWarningRead.value = true
  hideWorkerWarning.value = true
}

function getBrowserDisplayName(browser: string | null): string {
  if (browser == null) return 'Unknown'
  if (/chrome/i.test(browser)) return 'Google Chrome'
  if (/brave/i.test(browser)) return 'Brave'
  if (/opera/i.test(browser)) return 'Opera'
  if (/edge/i.test(browser)) return 'Microsoft Edge'
  if (/vivaldi/i.test(browser)) return 'Vivaldi'
  if (/firefox/i.test(browser)) return 'Mozilla Firefox'
  return 'Unknown'
}

function getBrowserIcon(browser: string | null): string {
  if (browser == null) return ''
  if (/chrome/i.test(browser)) return 'images/browsers/chrome.svg'
  if (/brave/i.test(browser)) return 'images/browsers/brave.svg'
  if (/opera/i.test(browser)) return 'images/browsers/opera.svg'
  if (/edge/i.test(browser)) return 'images/browsers/edge.svg'
  if (/vivaldi/i.test(browser)) return 'images/browsers/vivaldi.svg'
  if (/firefox/i.test(browser)) return 'images/browsers/firefox.svg'
  return ''
}

async function onManualRefresh(e: Event) {
  e.preventDefault()
  await refresh()
}

onMounted(async () => {
  await loadSettings()
  await refreshBrowserList()
  await checkRefreshNeeded()
  workerCountWarningRead.value = (await globalThis.workerCountWarningRead()) ?? false
})
</script>
