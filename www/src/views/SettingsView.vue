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
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Settings Sync</span>
        <span class="text-gray-500 text-xs italic ml-1">(optional - requires Discord)</span><br />

        <div v-if="cloudReachableChecking" class="mt-0_5rem text-gray-500 italic text-xs">
          Checking connection to ledgersync.davidarthurcole.me...
        </div>

        <div v-else-if="!cloudReachable" class="mt-0_5rem">
          <div class="border border-yellow-700 rounded-md px-3 py-2 text-yellow-400 text-xs space-y-1">
            <p class="font-semibold">Could not reach ledgersync.davidarthurcole.me</p>
            <p>Settings sync is unavailable right now. Possible causes:</p>
            <ul class="list-disc list-inside ml-1 text-yellow-400/80 space-y-0.5">
              <li>The server is protected by Cloudflare - VPNs or anonymising proxies may be blocked.</li>
              <li>Your network or firewall is blocking HTTPS to this host.</li>
              <li>The service is temporarily down.</li>
            </ul>
            <p class="text-yellow-400/70">Try disabling any active VPN, then click Retry below.</p>
          </div>
          <button
            type="button"
            class="apply-filter-button mt-0_5rem"
            @click="recheckReachable"
          >Retry</button>
        </div>

        <div v-else>
          <div class="mt-0_5rem">
            <template v-if="cloudConnected">
              <div class="flex items-center gap-2">
                <img
                  v-if="cloudAvatarUrl"
                  :src="cloudAvatarUrl"
                  class="w-7 h-7 rounded-full border border-gray-600"
                  alt="Discord avatar"
                />
                <div>
                  <span class="text-green-400 font-semibold">Connected</span>
                  <span v-if="cloudUsername" class="text-gray-400 ml-1">as <span class="text-gray-300">{{ cloudUsername }}</span></span>
                </div>
              </div>
              <div class="mt-0_25rem text-xs text-gray-500 space-y-0.5">
                <div>Last push: <span :class="cloudLastPushAt
                  ? (pushFlashing ? 'text-green-400 transition-none' : 'text-gray-400 transition-colors duration-[5000ms]')
                  : 'text-gray-600 italic'">{{ cloudLastPushAt ? cloudLastPushString : 'Never' }}</span></div>
                <div>Last pull: <span :class="cloudLastPullAt
                  ? (pullFlashing ? 'text-green-400 transition-none' : 'text-gray-400 transition-colors duration-[5000ms]')
                  : 'text-gray-600 italic'">{{ cloudLastPullAt ? cloudLastPullString : 'Never' }}</span></div>
                <div v-if="sessionExpiryString">Session expires: <span :class="sessionFlashing ? 'text-green-400 transition-none' : 'text-gray-400 transition-colors duration-[5000ms]'">{{ sessionExpiryString }}</span></div>
              </div>
              <div v-if="!cloudUsername || !cloudHasEncryptionKey" class="mt-0_5rem border border-yellow-700 rounded-md px-3 py-2 text-yellow-500 text-xs space-y-1.5 max-w-[50%]">
                <p class="italic">Reconnect to restore{{ !cloudHasEncryptionKey ? ' sync credentials' : ' display name and avatar' }}.</p>
                <button
                  type="button"
                  :disabled="cloudAuthWaiting"
                  class="apply-filter-button !mt-0 !text-white"
                  :style="{ backgroundColor: '#5865F2', borderColor: '#4752C4' }"
                  @click="connectDiscord"
                ><span class="inline-flex items-center gap-1.5"><svg class="w-4 h-4 flex-shrink-0" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057.101 18.079.11 18.1.133 18.114a19.929 19.929 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>{{ cloudAuthWaiting ? 'Waiting for auth...' : 'Reconnect with Discord' }}</span></button>
              </div>
            </template>
            <template v-else>
              <span class="text-gray-500 italic">Not connected</span>
            </template>
          </div>

          <div v-if="cloudAuthWaiting" class="mt-0_5rem border border-blue-700 rounded-md px-3 py-2 text-blue-300 text-xs max-w-[50%]">
            <p class="font-semibold">Waiting for Discord authorization...</p>
            <p class="text-blue-300/70 mt-0.5">Your browser should have opened the authorization page. Approve it there, then return here.</p>
            <p v-if="cloudAuthURL" class="mt-0.5">
              If nothing opened,
              <button type="button" class="underline text-blue-400 hover:text-blue-300" @click="openAuthURL">click here to open it manually</button>.
            </p>
          </div>

          <div class="mt-0_5rem flex flex-col gap-2">
            <button
              v-if="!cloudConnected"
              type="button"
              :disabled="cloudAuthWaiting"
              class="apply-filter-button !text-white self-start"
              :style="{ backgroundColor: '#5865F2', borderColor: '#4752C4' }"
              @click="connectDiscord"
            ><span class="inline-flex items-center gap-1.5"><svg class="w-4 h-4 flex-shrink-0" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057.101 18.079.11 18.1.133 18.114a19.929 19.929 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>{{ cloudAuthWaiting ? 'Waiting for auth...' : 'Connect with Discord' }}</span></button>

            <template v-if="cloudConnected">
              <div class="flex flex-row flex-wrap gap-2">
                <button
                  type="button"
                  :disabled="cloudSyncInProgress"
                  class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-blue-700 !text-white hover:!bg-blue-600 !border-blue-500"
                  @click="syncNow"
                ><span class="inline-flex items-center gap-1.5"><svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/></svg>{{ cloudSyncInProgress ? 'Syncing...' : 'Sync now' }}</span></button>
                <button
                  type="button"
                  :disabled="cloudRestoreInProgress"
                  class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-gray-700 !text-gray-200 hover:!bg-gray-600 !border-gray-500"
                  @click="restoreNow"
                ><span class="inline-flex items-center gap-1.5"><svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"/></svg>{{ cloudRestoreInProgress ? 'Restoring...' : 'Restore from cloud' }}</span></button>
              </div>
              <div class="flex flex-row flex-wrap gap-2">
                <template v-if="confirmingDeleteRemote">
                  <span class="text-xs text-red-400 self-center">Delete all remote data?</span>
                  <button
                    type="button"
                    class="apply-filter-button !mt-0 !mr-0 !ml-0 !text-gray-300 !bg-gray-700 hover:!bg-gray-600 !border-gray-500"
                    @click="confirmingDeleteRemote = false"
                  >Cancel</button>
                  <button
                    type="button"
                    :disabled="deleteRemoteInProgress"
                    class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-red-900 !text-red-200 hover:!bg-red-800 !border-red-700"
                    @click="deleteRemoteData"
                  >{{ deleteRemoteInProgress ? 'Deleting...' : 'Yes, delete' }}</button>
                </template>
                <template v-else>
                  <button
                    type="button"
                    class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-red-900 !text-red-200 hover:!bg-red-800 !border-red-700"
                    @click="disconnectCloud"
                  >Disconnect</button>
                  <button
                    type="button"
                    :disabled="deleteRemoteInProgress"
                    class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-red-900/60 !text-red-300 hover:!bg-red-900 !border-red-800"
                    @click="confirmingDeleteRemote = true"
                  >Delete Remote Data</button>
                </template>
              </div>
              <div class="flex items-center gap-2">
                <input
                  id="cloudAutoSyncCheckbox"
                  type="checkbox"
                  class="ext-opt-check"
                  v-model="cloudAutoSync"
                  @change="onAutoSyncChange"
                />
                <label for="cloudAutoSyncCheckbox" class="ext-opt-label">Auto-sync settings on change</label>
              </div>
            </template>
          </div>

          <div class="mt-0_5rem space-y-1">
            <div v-if="cloudSyncError" class="text-red-400 text-xs">Sync failed: {{ cloudSyncError }}</div>
            <div v-if="cloudRestoreError" class="text-red-400 text-xs">Restore failed: {{ cloudRestoreError }}</div>
            <div v-if="deleteRemoteError" class="text-red-400 text-xs">Delete failed: {{ deleteRemoteError }}</div>
            <div v-if="cloudSyncError && cloudSyncError.includes('session expired')" class="border border-yellow-700 rounded-md px-3 py-2 text-yellow-500 text-xs space-y-1.5 max-w-[50%]">
              <p class="italic">Your session has expired. Reconnect to continue syncing.</p>
              <button
                type="button"
                :disabled="cloudAuthWaiting"
                class="apply-filter-button !mt-0 !text-white"
                :style="{ backgroundColor: '#5865F2', borderColor: '#4752C4' }"
                @click="connectDiscord"
              ><span class="inline-flex items-center gap-1.5"><svg class="w-4 h-4 flex-shrink-0" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057.101 18.079.11 18.1.133 18.114a19.929 19.929 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>{{ cloudAuthWaiting ? 'Waiting for auth...' : 'Reconnect with Discord' }}</span></button>
            </div>
            <div v-if="cloudRestoreError && cloudRestoreError.includes('session expired')" class="border border-yellow-700 rounded-md px-3 py-2 text-yellow-500 text-xs space-y-1.5 max-w-[50%]">
              <p class="italic">Your session has expired. Reconnect to continue syncing.</p>
              <button
                type="button"
                :disabled="cloudAuthWaiting"
                class="apply-filter-button !mt-0 !text-white"
                :style="{ backgroundColor: '#5865F2', borderColor: '#4752C4' }"
                @click="connectDiscord"
              ><span class="inline-flex items-center gap-1.5"><svg class="w-4 h-4 flex-shrink-0" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057.101 18.079.11 18.1.133 18.114a19.929 19.929 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>{{ cloudAuthWaiting ? 'Waiting for auth...' : 'Reconnect with Discord' }}</span></button>
            </div>
            <div v-if="cloudRestoreSuccess" class="text-green-400 text-xs">Restore complete. Restart the app to apply synced settings.</div>
          </div>
        </div>
      </div>

      <hr class="mt-1rem mb-1rem w-full" />

      <div class="px-2 py-2 text-sm text-gray-400 bg-darker rounded-md tabular-nums overflow-auto">
        <span class="mr-0_5rem font-bold text-lg text-gray-400 ledger-underline">Storage</span><br />
        <div class="mt-0_5rem">
          <span class="section-heading">Storage Location</span><br />
          <div class="mt-0_5rem pl-0_5rem">
            <div class="flex flex-row items-center gap-2">
              <input
                type="text"
                readonly
                :value="storagePath || 'Loading...'"
                :size="(storagePath || 'Loading...').length + 2"
                class="block rounded-md text-sm bg-darkest font-mono text-gray-300 cursor-default border border-gray-600 px-2 py-1 focus:outline-none focus:ring-0 shrink min-w-0"
              />
              <button
                type="button"
                class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-blue-700 !text-white hover:!bg-blue-600 !border-blue-500 flex-shrink-0"
                @click="openStorageFolder"
              >Open folder</button>
              <button
                type="button"
                class="apply-filter-button !mt-0 !mr-0 !ml-0 flex-shrink-0"
                :class="storageFolderHidden
                  ? '!bg-green-700 !text-white hover:!bg-green-600 !border-green-500'
                  : '!bg-orange-700 !text-white hover:!bg-orange-600 !border-orange-500'"
                @click="toggleStorageVisible"
              >{{ storageFolderHidden ? 'Show folder' : 'Hide folder' }}</button>
            </div>
          </div>
        </div>

        <div class="mt-1rem">
          <span class="section-heading">Backup Storage</span><br />
          <div class="mt-0_25rem pl-0_5rem text-xs text-gray-500">
            Copies all app data into the chosen folder. The original files are not modified.
          </div>
          <div class="mt-0_5rem pl-0_5rem">
            <div class="flex flex-row items-center gap-2 max-w-[50%]">
              <input
                type="text"
                class="drop-select-full text-sm bg-darkest"
                placeholder="Destination path..."
                v-model="backupDestPath"
                @change="onBackupDestChange"
              />
              <button
                type="button"
                class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-gray-700 !text-gray-200 hover:!bg-gray-600 !border-gray-500 flex-shrink-0"
                @click="chooseBackupDest"
              >Choose...</button>
              <button
                type="button"
                class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-blue-700 !text-white hover:!bg-blue-600 !border-blue-500 flex-shrink-0"
                :disabled="!backupDestPath || backupInProgress || (!backupDb && !backupExports && !backupLogs)"
                @click="runBackup"
              >{{ backupInProgress ? 'Backing up...' : 'Backup storage' }}</button>
            </div>
            <div class="mt-0_5rem flex items-center gap-4 max-w-[50%]">
              <label class="flex items-center gap-1 cursor-pointer select-none text-xs text-gray-400">
                <input type="checkbox" class="ext-opt-check" v-model="backupDb" />
                Database
              </label>
              <label class="flex items-center gap-1 cursor-pointer select-none text-xs text-gray-400">
                <input type="checkbox" class="ext-opt-check" v-model="backupExports" />
                Exports
              </label>
              <label class="flex items-center gap-1 cursor-pointer select-none text-xs text-gray-400">
                <input type="checkbox" class="ext-opt-check" v-model="backupLogs" />
                Logs
              </label>
            </div>
            <div class="mt-0_5rem max-w-[50%]">
              <SegmentedProgressBar
                :active="backupInProgress"
                :segments="backupSegments"
                :status-text="backupSuccess ? 'Backup complete.' : backupError || undefined"
                :status-class="backupError ? 'text-red-400' : 'text-green-400'"
                :is-spinning="backupInProgress"
              />
            </div>
          </div>
        </div>

        <div class="mt-1rem">
          <span class="section-heading">Move Storage</span><br />
          <div class="mt-0_25rem pl-0_5rem text-xs text-gray-500">
            Copies all app data (databases, exports, logs) into the chosen folder, then restarts. The old files are NOT deleted automatically.
          </div>
          <div class="mt-0_5rem pl-0_5rem">
            <div class="flex flex-row items-center gap-2 max-w-[50%]">
              <input
                type="text"
                class="drop-select-full text-sm bg-darkest"
                placeholder="Destination path..."
                v-model="moveDestPath"
              />
              <button
                type="button"
                class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-gray-700 !text-gray-200 hover:!bg-gray-600 !border-gray-500 flex-shrink-0"
                @click="chooseMoveDest"
              >Choose...</button>
              <button
                type="button"
                class="apply-filter-button !mt-0 !mr-0 !ml-0 !bg-orange-700 !text-white hover:!bg-orange-600 !border-orange-500 flex-shrink-0"
                :disabled="!moveDestPath || moveInProgress"
                @click="runMove"
              >{{ moveInProgress ? 'Moving...' : 'Move storage' }}</button>
              <span v-if="moveError" class="text-red-400 text-xs flex-shrink-0">{{ moveError }}</span>
            </div>
          </div>
        </div>
        <div class="mt-1rem pt-1rem border-t border-gray-600">
          <span class="section-heading">Export</span><br />
          <div class="mt-0_5rem">
            <input
              id="autoExportCsvCheckbox"
              type="checkbox"
              class="ext-opt-check mr-0_5rem"
              v-model="autoExportCsv"
            />
            <label for="autoExportCsvCheckbox" class="ext-opt-label">Auto-export CSV on fetch</label>
          </div>
          <div class="mt-0_5rem">
            <input
              id="autoExportXlsxCheckbox"
              type="checkbox"
              class="ext-opt-check mr-0_5rem"
              v-model="autoExportXlsx"
            />
            <label for="autoExportXlsxCheckbox" class="ext-opt-label">Auto-export XLSX on fetch</label>
          </div>
          <ExportManagementPanel />
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
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import type { Ref } from 'vue'
import { useSettings } from '../composables/useSettings'
import { useMennoData } from '../composables/useMennoData'
import { useDropdownSelector } from '../composables/useDropdownSelector'
import SegmentedProgressBar from '../components/SegmentedProgressBar.vue'
import type { ProgressSegment } from '../components/SegmentedProgressBar.vue'
import ExportManagementPanel from '../components/ExportManagementPanel.vue'

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
  autoExportCsv,
  autoExportXlsx,
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

const storagePath = ref('')
const storageFolderHidden = ref(true)
const backupDestPath = ref('')
const backupInProgress = ref(false)
const backupSuccess = ref(false)
const backupError = ref('')
const moveDestPath = ref('')
const moveInProgress = ref(false)
const moveError = ref('')

const pendingSegment = (label: string): ProgressSegment => ({ label, status: 'pending', color: 'blue' })
const activeSegment = (label: string): ProgressSegment => ({ label, status: 'active', color: 'blue', pulsing: true })
const doneSegment = (label: string): ProgressSegment => ({ label, status: 'done', color: 'blue' })
const failedSegment = (label: string): ProgressSegment => ({ label, status: 'failed', color: 'blue' })

const backupDb = ref(true)
const backupExports = ref(true)
const backupLogs = ref(true)

const backupSegments = ref<ProgressSegment[]>([
  pendingSegment('Database'),
  pendingSegment('Exports'),
  pendingSegment('Logs'),
])

function openStorageFolder() {
  if (storagePath.value) {
    globalThis.openFileInFolder(storagePath.value)
  }
}

async function toggleStorageVisible() {
  const nowHidden = !storageFolderHidden.value
  storageFolderHidden.value = nowHidden
  await globalThis.setStorageFolderVisible(!nowHidden)
}

function onBackupDestChange() {
  globalThis.setBackupDestPath(backupDestPath.value)
}

async function chooseBackupDest() {
  const path = await globalThis.chooseFolderPath()
  if (path) {
    backupDestPath.value = path
    globalThis.setBackupDestPath(path)
  }
}

async function chooseMoveDest() {
  const path = await globalThis.chooseFolderPath()
  if (path) moveDestPath.value = path
}

async function runBackup() {
  backupSuccess.value = false
  backupError.value = ''
  backupInProgress.value = true
  const dest = backupDestPath.value
  const allParts: Array<{ part: 'internal' | 'exports' | 'logs'; label: string; enabled: boolean }> = [
    { part: 'internal', label: 'Database', enabled: backupDb.value },
    { part: 'exports', label: 'Exports', enabled: backupExports.value },
    { part: 'logs', label: 'Logs', enabled: backupLogs.value },
  ]
  const parts = allParts.filter(p => p.enabled)
  backupSegments.value = parts.map((p, i) => i === 0 ? activeSegment(p.label) : pendingSegment(p.label))
  try {
    for (let i = 0; i < parts.length; i++) {
      await globalThis.backupStoragePart(dest, parts[i].part)
      backupSegments.value[i] = doneSegment(parts[i].label)
      if (i + 1 < parts.length) {
        backupSegments.value[i + 1] = activeSegment(parts[i + 1].label)
      }
    }
    backupSuccess.value = true
  } catch (e: unknown) {
    backupError.value = e instanceof Error ? e.message : String(e)
    const activeIdx = backupSegments.value.findIndex(s => s.status === 'active')
    if (activeIdx >= 0) backupSegments.value[activeIdx] = failedSegment(parts[activeIdx].label)
  } finally {
    backupInProgress.value = false
  }
}

async function runMove() {
  moveError.value = ''
  moveInProgress.value = true
  try {
    await globalThis.moveStorageTo(moveDestPath.value)
  } catch (e: unknown) {
    moveError.value = e instanceof Error ? e.message : String(e)
    moveInProgress.value = false
  }
}

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
  storagePath.value = (await globalThis.getStoragePath()) ?? ''
  backupDestPath.value = (await globalThis.getBackupDestPath()) ?? ''
  cloudAutoSync.value = (await globalThis.getCloudAutoSync()) ?? false
  await initCloudSync()
})

onUnmounted(() => {
  globalThis.removeEventListener('resize', onResize)
  globalThis.onDiscordAuthComplete = () => {}
  globalThis.onCloudSyncComplete = () => {}
  globalThis.onCloudRestoreComplete = () => {}
})

// Cloud sync state
const cloudReachableChecking = ref(false)
const cloudReachable = ref(false)
const cloudConnected = ref(false)
const cloudUsername = ref('')
const cloudAvatarUrl = ref('')
const cloudHasEncryptionKey = ref(false)
const cloudLastPushAt = ref(0)
const cloudLastPullAt = ref(0)
const cloudAuthWaiting = ref(false)
const cloudAuthURL = ref('')
const cloudSyncInProgress = ref(false)
const cloudSyncError = ref('')
const cloudRestoreInProgress = ref(false)
const cloudRestoreError = ref('')
const cloudRestoreSuccess = ref(false)
const confirmingDeleteRemote = ref(false)
const deleteRemoteInProgress = ref(false)
const deleteRemoteError = ref('')
const cloudAutoSync = ref(false)

const cloudLastPushString = computed(() => {
  if (!cloudLastPushAt.value) return ''
  return new Date(cloudLastPushAt.value * 1000).toLocaleString()
})

const cloudLastPullString = computed(() => {
  if (!cloudLastPullAt.value) return ''
  return new Date(cloudLastPullAt.value * 1000).toLocaleString()
})

const sessionExpiryString = computed(() => {
  const lastActivity = Math.max(cloudLastPushAt.value, cloudLastPullAt.value)
  if (!lastActivity) return ''
  const expiryMs = (lastActivity + 30 * 24 * 3600) * 1000
  return new Date(expiryMs).toLocaleString()
})

const pushFlashing = ref(false)
const pullFlashing = ref(false)
const sessionFlashing = ref(false)

function triggerFlash(flashRef: Ref<boolean>) {
  flashRef.value = true
  nextTick(() => {
    requestAnimationFrame(() => { flashRef.value = false })
  })
}

watch(cloudLastPushAt, () => {
  triggerFlash(pushFlashing)
  triggerFlash(sessionFlashing)
})
watch(cloudLastPullAt, () => {
  triggerFlash(pullFlashing)
  triggerFlash(sessionFlashing)
})

type CloudStatusPayload = { connected: boolean; username: string; avatarUrl: string; lastPushAt: number; lastPullAt: number; hasEncryptionKey: boolean }

function applyCloudStatus(status: CloudStatusPayload) {
  cloudConnected.value = status.connected
  cloudUsername.value = status.username ?? ''
  cloudAvatarUrl.value = status.avatarUrl ?? ''
  cloudLastPushAt.value = status.lastPushAt ?? 0
  cloudLastPullAt.value = status.lastPullAt ?? 0
  cloudHasEncryptionKey.value = status.hasEncryptionKey ?? false
}

async function initCloudSync() {
  applyCloudStatus(JSON.parse(await globalThis.getCloudSyncStatus()))

  cloudReachableChecking.value = true
  cloudReachable.value = await globalThis.checkCloudReachable()
  cloudReachableChecking.value = false
  if (!cloudReachable.value) return

  globalThis.onDiscordAuthComplete = (connected: boolean, username: string) => {
    cloudAuthWaiting.value = false
    cloudConnected.value = connected
    cloudUsername.value = username ?? ''
    if (connected) {
      cloudSyncError.value = ''
      cloudRestoreError.value = ''
      refreshSyncStatus()
    }
  }
  globalThis.onCloudSyncComplete = (success: boolean, errMsg: string) => {
    cloudSyncInProgress.value = false
    if (success) {
      cloudSyncError.value = ''
      refreshSyncStatus()
    } else {
      cloudSyncError.value = errMsg
    }
  }
  globalThis.onCloudRestoreComplete = (success: boolean, errMsg: string) => {
    cloudRestoreInProgress.value = false
    if (success) {
      cloudRestoreSuccess.value = true
      cloudRestoreError.value = ''
    } else {
      cloudRestoreError.value = errMsg
    }
  }
}

async function recheckReachable() {
  cloudReachableChecking.value = true
  cloudReachable.value = await globalThis.checkCloudReachable()
  cloudReachableChecking.value = false
  if (cloudReachable.value) {
    await initCloudSync()
  }
}

async function refreshSyncStatus() {
  applyCloudStatus(JSON.parse(await globalThis.getCloudSyncStatus()))
}

async function connectDiscord() {
  cloudAuthWaiting.value = true
  cloudAuthURL.value = ''
  try {
    const url = await globalThis.connectDiscord()
    cloudAuthURL.value = url ?? ''
  } catch (_e: unknown) {
    console.error('Error initiating Discord connection:', _e)
    cloudAuthWaiting.value = false
  }
}

function openAuthURL() {
  if (cloudAuthURL.value) {
    globalThis.openURL(cloudAuthURL.value)
  }
}

async function disconnectCloud() {
  await globalThis.disconnectCloud()
  cloudConnected.value = false
  cloudUsername.value = ''
  cloudAvatarUrl.value = ''
  cloudLastPushAt.value = 0
  cloudLastPullAt.value = 0
}

function syncNow() {
  cloudSyncInProgress.value = true
  cloudSyncError.value = ''
  globalThis.syncToCloud()
}

function restoreNow() {
  cloudRestoreInProgress.value = true
  cloudRestoreError.value = ''
  cloudRestoreSuccess.value = false
  globalThis.restoreFromCloud()
}

async function deleteRemoteData() {
  deleteRemoteInProgress.value = true
  deleteRemoteError.value = ''
  confirmingDeleteRemote.value = false
  const errMsg = await globalThis.deleteRemoteData()
  deleteRemoteInProgress.value = false
  if (errMsg) {
    deleteRemoteError.value = errMsg
  }
}

async function onAutoSyncChange() {
  await globalThis.setCloudAutoSync(cloudAutoSync.value)
}

watch(
  [
    autoRefreshMenno, autoRetry, hideTimeoutErrors, workerCount,
    screenshotSafety, showMissionProgress, collapseOlderSections,
  ],
  () => {
    if (cloudAutoSync.value && cloudConnected.value && !cloudSyncInProgress.value) {
      syncNow()
    }
  },
)
</script>
