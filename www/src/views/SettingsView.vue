<template>
  <div class="view-layout overflow-y-scroll">
    <div class="flex-1 min-h-0 px-3 py-2 overflow-auto shadow-sm block text-sm font-mono text-gray-400 bg-darkest rounded-md">
      <div class="px-2 py-2 text-sm text-gray-400 bg-darker rounded-md tabular-nums overflow-auto">
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Window &amp; Browser</span>
        <span class="text-gray-500 text-xs italic ml-1">(effective on restart)</span><br />
        <div class="mt-0_5rem">
          <span class="section-heading">Preferred Browser</span><br />
          <div ref="containerRef" class="text-sm relative w-full flex-grow focus-within:z-10 pl-0_5rem">
            <div v-if="preferredBrowser" class="ledger-input-overlay" style="padding-left: 1.25rem;">
              <span>{{ preferredBrowser }}</span> (<img v-if="getBrowserIcon(preferredBrowser)" :src="getBrowserIcon(preferredBrowser)" :alt="getBrowserDisplayName(preferredBrowser)" class="inline-block w-4 h-4 align-text-bottom mr-1" /><span class="text-gray-300">{{ getBrowserDisplayName(preferredBrowser) }}</span>)
            </div>
            <input
              id="preferredBrowserInput"
              type="text"
              class="drop-select-full text-sm bg-darkest"
              :style="preferredBrowser ? { color: 'transparent' } : undefined"
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
                class="drop-opt bg-darkest"
                @click="closePrefBrowserDropdown(browser)"
              >
                {{ browser }} <span class="inline-block max-w-9/10">(<img v-if="getBrowserIcon(browser)" :src="getBrowserIcon(browser)" :alt="getBrowserDisplayName(browser)" class="inline-block w-4 h-4 align-text-bottom mr-1" /><span class="text-gray-300">{{ getBrowserDisplayName(browser) }}</span>)</span>
              </li>
            </ul>
          </div>
          <div v-if="preferredBrowser && preferredBrowser !== loadedBrowser" class="mt-0_5rem pl-0_5rem">
            <button
              type="button"
              class="apply-filter-button !bg-blue-700 !text-white hover:!bg-blue-600 !border-blue-500"
              @click="restartApp()"
            >Restart Now</button>
            <span class="ml-0_5rem text-gray-400 text-xs">Browser change takes effect after restart</span>
          </div>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">App Resolution</span><br />
          <div class="flex flex-row items-center pl-0_5rem">
            <div class="flex flex-col w-full max-w-10rem text-center mt-0_5rem">
              <input type="number" id="resPrefX" class="number-input text-sm bg-darkest" v-model="resolutionX" />
              <span class="text-sm text-gray-400 mt-0_5rem">Width<br />(<span class="text-gray-300">X</span>)</span>
            </div>
            <span class="text-xl font-bold ml-0_5rem mr-0_5rem mb-2_5rem">x</span>
            <div class="flex flex-col w-full max-w-10rem text-center mt-0_5rem">
              <input type="number" id="resPrefY" class="number-input text-sm bg-darkest" v-model="resolutionY" />
              <span class="text-sm text-gray-400 mt-0_5rem">Height<br />(<span class="text-gray-300">Y</span>)</span>
            </div>
            <span class="text-xl font-bold ml-0_5rem mr-0_5rem mb-2_5rem">@</span>
            <div class="flex flex-col w-full max-w-5rem text-center mt-0_5rem">
              <input type="number" id="scalePref" class="number-input text-sm bg-darkest" v-model="scalingFactor" />
              <span class="text-sm text-gray-400 mt-0_5rem">Scaling Factor</span>
            </div>
            <button
              v-if="captureButtonActive"
              class="apply-filter-button !bg-blue-700 !text-white hover:!bg-blue-600 !border-blue-500 !ml-1rem mb-2_5rem !mt-0 !mr-0"
              @click="captureFromCurrent"
            >Capture from Current</button>
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
        <div v-if="autoRefreshMenno" class="mt-0_5rem text-xs text-gray-500 italic">
          <span v-if="nextWeeklyRefreshString">Next run after {{ nextWeeklyRefreshString }}</span>
          <span v-else>Will run on next launch</span>
        </div>
      </div>

      <hr class="mt-1rem mb-1rem w-full" />

      <div class="px-2 py-2 text-sm text-gray-400 bg-darker rounded-md tabular-nums overflow-auto">
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Fetch Behavior</span><br />
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
          <span class="section-heading">Parallel Download Workers</span><br />
          <div class="mt-0_5rem pl-0_5rem">
            <!-- Worker count callout above selected segment -->
            <div class="relative h-7 mb-1 select-none w-4/5">
              <span
                class="absolute top-0 text-sm font-bold font-mono -translate-x-1/2"
                :style="{ left: `${(workerCount - 0.5) * 10}%` }"
                :class="workerCount <= 4 ? 'text-green-400' : workerCount <= 7 ? 'text-orange-400' : 'text-red-400'"
              >{{ workerCount }}</span>
              <!-- |─── range indicator -->
              <div
                class="absolute bottom-0 h-2.5 border-b border-l pointer-events-none opacity-70"
                :style="{ left: '0', width: `${(workerCount - 0.5) * 10}%` }"
                :class="workerCount <= 4 ? 'border-green-400' : workerCount <= 7 ? 'border-orange-400' : 'border-red-400'"
              ></div>
            </div>
            <div class="flex gap-px w-4/5 cursor-pointer">
              <div
                v-for="i in 10"
                :key="i"
                class="flex-1 h-4 rounded-sm"
                :class="[
                  i <= 4 ? 'bg-green-600' : i <= 7 ? 'bg-orange-500' : 'bg-red-600',
                  i > workerCount ? 'opacity-20' : '',
                ]"
                @click="workerCount = i"
              ></div>
            </div>
            <div class="flex text-xs mt-0_25rem select-none w-4/5">
              <span class="text-green-500 text-center" style="width: 40%">Safe</span>
              <span class="text-orange-400 text-center" style="width: 30%">Caution</span>
              <span class="text-red-500 text-center" style="width: 30%">Risky</span>
            </div>
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

      <hr class="mt-1rem mb-1rem w-full" />

      <div class="px-2 py-2 text-sm text-gray-400 bg-darker rounded-md tabular-nums overflow-auto">
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Display</span><br />
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
          <span class="section-heading">Mission Progress Panel</span><br />
          <input
            id="showMissionProgressCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="showMissionProgress"
          />
          <label for="showMissionProgressCheckbox" class="ext-opt-label">Show individual mission progress during fetching</label>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">Mission Data Default Collapse</span><br />
          <input
            id="collapseOlderSectionsCheckbox"
            type="checkbox"
            class="ext-opt-check mr-0_5rem"
            v-model="collapseOlderSections"
          />
          <label for="collapseOlderSectionsCheckbox" class="ext-opt-label">Collapse all but the most recent year section by default</label>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useSettings } from '../composables/useSettings'
import { useMennoData } from '../composables/useMennoData'
import { useDropdownSelector } from '../composables/useDropdownSelector'

const {
  resolutionX,
  resolutionY,
  scalingFactor,
  startInFullscreen,
  preferredBrowser,
  loadedBrowser,
  allBrowsers,
  autoRefreshMenno,
  autoRetry,
  hideTimeoutErrors,
  workerCount,
  screenshotSafety,
  showMissionProgress,
  collapseOlderSections,
  loadSettings,
  setPreferredBrowser,
  refreshBrowserList,
} = useSettings()

const { secondsSinceLastUpdate, lastUpdateString, nextWeeklyRefreshString, refresh, checkRefreshNeeded } = useMennoData()

const {
  containerRef,
  isOpen: prefBrowserDropdownOpen,
  open: openPrefBrowserDropdown,
  close: closePrefBrowserDropdown,
} = useDropdownSelector((pref) => { setPreferredBrowser(pref) })

const workerCountWarningRead = ref(false)
const hideWorkerWarning = ref(false)

const windowOuterWidth = ref(globalThis.outerWidth)
const windowOuterHeight = ref(globalThis.outerHeight)
const windowDpr = ref(globalThis.devicePixelRatio)

function onResize() {
  windowOuterWidth.value = globalThis.outerWidth
  windowOuterHeight.value = globalThis.outerHeight
  windowDpr.value = globalThis.devicePixelRatio
}

const captureButtonActive = computed(() =>
  resolutionX.value !== windowOuterWidth.value ||
  resolutionY.value !== windowOuterHeight.value ||
  scalingFactor.value !== windowDpr.value,
)

function captureFromCurrent() {
  resolutionX.value = windowOuterWidth.value
  resolutionY.value = windowOuterHeight.value
  scalingFactor.value = globalThis.devicePixelRatio
}

function restartApp() {
  globalThis.restartApp()
}

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
  globalThis.addEventListener('resize', onResize)
  await loadSettings()
  await refreshBrowserList()
  await checkRefreshNeeded()
  workerCountWarningRead.value = (await globalThis.workerCountWarningRead()) ?? false
})

onUnmounted(() => {
  globalThis.removeEventListener('resize', onResize)
})
</script>
