<template>
  <div class="flex-1 w-full mx-auto px-4 overflow-y-auto">
    <div class="text-sm text-gray-400 space-y-2 pb-3">

      <!-- Support banner -->
      <div v-if="!coffeeDismissed" class="flex items-start gap-3 p-3 rounded-lg bg-yellow-950/40 border border-yellow-700/40 mt-2">
        <div class="flex-1 space-y-1">
          <p class="text-yellow-200/70 text-xs">EggLedger is free - if it's saved you time, consider supporting development!</p>
          <p class="text-yellow-200/50 text-xs">Getting $10 helps cover Azure Windows signing costs.<br />As a stretch goal, enough for a $99/yr Apple license to properly sign the app on macOS.</p>
        </div>
        <div class="flex flex-col items-end gap-2 flex-shrink-0">
          <a
            v-external-link
            href="https://buymeacoffee.com/davidarthurcole"
            target="_blank"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-yellow-400 hover:bg-yellow-300 text-gray-900 text-xs font-semibold transition-colors"
          >&#x2615; Buy me a coffee</a>
          <a
            v-external-link
            href="https://github.com/sponsors/DavidArthurCole"
            target="_blank"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-gray-700 hover:bg-gray-600 text-gray-200 text-xs font-semibold transition-colors"
          >&#10084; GitHub Sponsors</a>
          <a
            v-external-link
            href="https://patreon.com/DavidArthurCole"
            target="_blank"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-orange-700 hover:bg-orange-600 text-white text-xs font-semibold transition-colors"
          >&#9733; Patreon</a>
          <button class="text-yellow-200/30 hover:text-yellow-200/60 text-xs" type="button" @click="dismissCoffee">Dismiss</button>
        </div>
      </div>

      <!-- Update banner -->
      <div v-if="appHasUpdate" class="flex items-center gap-3 p-3 rounded-lg bg-blue-950/40 border border-blue-700/40 mt-2">
        <div class="flex-1 space-y-1">
          <p class="text-blue-200/80 text-xs font-semibold">Update v{{ appHasUpdate }} available</p>
          <div v-if="updateInProgress">
            <p class="text-blue-200/60 text-xs">
              Downloading...
              <span v-if="updateProgress.total > 0">{{ Math.round(updateProgress.downloaded / updateProgress.total * 100) }}%</span>
            </p>
          </div>
          <p v-if="updateError" class="text-red-400 text-xs">{{ updateError }}</p>
        </div>
        <button
          v-if="!updateInProgress"
          type="button"
          class="flex-shrink-0 inline-flex items-center px-3 py-1.5 rounded-md bg-blue-600 hover:bg-blue-500 text-white text-xs font-semibold transition-colors"
          @click="startUpdate"
        >Update now</button>
      </div>

      <!-- Pane grid -->
      <div class="grid grid-cols-2 gap-3 mt-2">

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Is mk2 back?</h2>
          <p>No. This tool is now being developed independently - the community has taken over mk2's orphaned tools since he went AWOL in 2022.</p>
          <p>
            <i>For EggLedger specifically</i>, I (<a v-external-link href="https://github.com/DavidArthurCole" target="_blank" class="url-link">DavidArthurCole</a>) am the active developer and maintainer.
          </p>
          <p>
            Reach out on <a v-external-link href="https://discord.com/users/258383847148879874" target="_blank" class="url-link">Discord</a> (<span class="text-duration-0">@davidarthurcole</span>) or <a v-external-link href="https://github.com/DavidArthurCole/EggLedger/issues/new/choose" target="_blank" class="url-link">open a GitHub Issue</a> for bugs, requests, or questions.
          </p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">What does EggLedger do?</h2>
          <p>
            Exports spaceship mission data - including loot from each mission - to .xlsx and .csv for further analysis.
          </p>
          <p>
            It extends the <a v-external-link href="https://wasmegg-carpet.netlify.app/rockets-tracker/" target="_blank" class="url-link">rockets tracker</a> to answer questions like "which mission dropped this artifact?" and "how many times did this item drop?" that can't be answered there.
          </p>
          <p>It also lets you browse and filter mission data in-app.</p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">How do I use EggLedger?</h2>
          <ul class="about-list-1">
            <li>Go to the <b>Ledger</b> tab</li>
            <li>Enter your Egg, Inc. account ID</li>
            <li>Press <b>Fetch</b>
              <ul class="about-list-2">
                <li>First fetch may take a while if you have many completed missions</li>
                <li>Subsequent fetches only pull new missions</li>
              </ul>
            </li>
          </ul>
          <p class="ledger-underline">Data for multiple accounts can coexist.</p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Where do I find my Egg, Inc. account ID?</h2>
          <ul class="about-list-1">
            <li>Open the game menu <img :src="'images/icon_menu.webp'" alt="Menu Icon" class="game-btn-ico" /></li>
            <li>Choose Settings <img :src="'images/icon_settings.webp'" alt="Settings Icon" class="game-btn-ico" /></li>
            <li>Choose &nbsp;<span class="p-0.5 text-xs rounded-md bg-privacy_blue w-4 pl-3 pr-3"> Privacy &amp; Data </span></li>
            <li>Your EID is at the bottom
              <ul class="about-list-2">
                <li><span class="ledger-underline">It should look like EI1234567890123456</span>. That's the letters "EI" followed by 16 digits.</li>
                <li>Alternatively, if you choose Help <img :src="'images/icon_help.webp'" alt="Help Icon" class="game-btn-ico" /> in the main menu and click on &nbsp;<span class="p-0.5 text-xs rounded-md bg-data_loss_red w-4 pl-3 pr-3"> Data Loss Issue </span>&nbsp; the game will auto-compose an email with your ID in the subject.</li>
              </ul>
            </li>
          </ul>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Is my data shared with anyone?</h2>
          <p>No. EggLedger talks directly to the Egg, Inc. API - all data stays 100% local. No analytics are collected.</p>
          <p>The only third-party request is an occasional update check against github.com. No personal data is attached and no logs are available to me.</p>
          <p>Unless you contact me directly, I have no way to know you're even using this tool.</p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Are there account risks?</h2>
          <p>I'm not aware of any negative effects. The <a v-external-link href="https://wasmegg-carpet.netlify.app/rockets-tracker/" target="_blank" class="url-link">rockets tracker</a> has safely used the same techniques for years.</p>
          <p>That said, no community tools are sanctioned by the Egg, Inc. developer. You use them at your own risk.</p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Why are recent missions missing?</h2>
          <p>Your server backup is probably out of date. Watch the freshness message when your backup is fetched.</p>
          <p>The game backs up every few minutes when network allows, but it's unpredictable. <span class="ledger-underline">Force-closing and reopening the game, or switching farms, reliably triggers a new backup.</span></p>
          <p>Even after a backup, it may take a minute or two before the API serves the updated data.</p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Where does EggLedger store data?</h2>
          <p>All config, data, and exports are stored in the same directory as the EggLedger binary.</p>
          <p><span class="ledger-underline">Put EggLedger in a directory of its own and don't touch the "internal" folder</span> - doing so risks data corruption.</p>
          <p>To uninstall, just delete the directory. No system-level traces are left behind.</p>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Known issues</h2>
          <ul class="about-list-1">
            <li>On macOS, clicking the dock icon can open a new browser window</li>
            <li>On Windows, the taskbar icon resolution is low</li>
            <li>Some missions may fail to fetch
              <ul class="about-list-2">
                <li>Occasional API timeouts are normal - a second fetch usually resolves them</li>
                <li>If the same mission fails repeatedly, please reach out</li>
              </ul>
            </li>
            <li>The Mission Data tab can be slow with very large mission histories</li>
          </ul>
        </div>

        <div class="bg-dark rounded-md p-3 space-y-1">
          <h2 class="font-bold text-gray-300 border-b border-gray-700 pb-1 mb-2">Where do I get @mk2's autograph?</h2>
          <p><a v-external-link href="https://areyousure.netlify.app/" target="_blank" class="url-link">Are you sure</a> you want @mk2's autograph?</p>
        </div>

      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useAppState } from '../composables/useAppState'

const { appHasUpdate } = useAppState()

const COFFEE_DISMISS_KEY = 'eggl_coffee_dismissed'
const coffeeDismissed = ref(localStorage.getItem(COFFEE_DISMISS_KEY) === '1')

function dismissCoffee() {
  localStorage.setItem(COFFEE_DISMISS_KEY, '1')
  coffeeDismissed.value = true
}

const updateInProgress = ref(false)
const updateProgress = ref({ downloaded: 0, total: 0 })
const updateError = ref('')

async function startUpdate() {
  if (!appHasUpdate.value) return
  updateInProgress.value = true
  updateError.value = ''
  globalThis.updateDownloadProgress = (downloaded, total) => {
    updateProgress.value = { downloaded, total }
  }
  try {
    await globalThis.downloadAndInstallUpdate(appHasUpdate.value)
    // App will restart - no further action needed
  } catch (e: unknown) {
    updateError.value = e instanceof Error ? e.message : String(e)
    updateInProgress.value = false
  }
}
</script>
