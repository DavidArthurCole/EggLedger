<template>
  <div v-show="mounted" class="h-full flex flex-col space-y-3 pb-3 bg-darker">
    <TabBar :tabs="tabList" v-model:active-tab="activeTab" />

    <LedgerView v-show="activeTab === 'Ledger'" />
    <MissionDataView v-show="activeTab === 'Mission Data'" />
    <LifetimeDataView v-show="activeTab === 'Lifetime Data'" />
    <SettingsView v-show="activeTab === 'Settings'" />
    <AboutView v-show="activeTab === 'About'" />

    <footer class="flex-shrink-0 text-center text-sm text-gray-500">
      <a v-external-link href="https://github.com/DavidArthurCole/EggLedger" target="_blank" class="url-link">EggLedger</a>
      v{{ appVersion }} by @<a v-external-link href="https://github.com/fanaticscripter" target="_blank" class="url-link">mk2</a>
      &amp; @<a v-external-link href="https://github.com/DavidArthurCole" target="_blank" class="url-link">DavidArthurCole</a>
      <span v-if="appHasUpdate" class="text-red-700">
        (<button class="text-red-700 hover:text-red-800 ledger-underline" @click="updateModalDismissed = false">New version available!</button>)
      </span>
    </footer>

    <UpdateModal
      :visible="!!appHasUpdate && !updateModalDismissed"
      :release-tag="appHasUpdate"
      :release-notes="appReleaseNotes"
      @close="updateModalDismissed = true"
    />
  </div>
  <MennoLoadingModal
    :visible="mennoRefreshing"
    :is-auto-refresh="mennoIsAutoRefresh"
    :progress="mennoProgress"
  />
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import TabBar from './components/TabBar.vue'
import SettingsView from './views/SettingsView.vue'
import LedgerView from './views/LedgerView.vue'
import MissionDataView from './views/MissionDataView.vue'
import LifetimeDataView from './views/LifetimeDataView.vue'
import AboutView from './views/AboutView.vue'
import UpdateModal from './components/modals/UpdateModal.vue'
import MennoLoadingModal from './components/modals/MennoLoadingModal.vue'
import { useAppState } from './composables/useAppState'
import { useMennoData } from './composables/useMennoData'
import { registerShortcuts } from './shortcuts'

const {
  activeTab,
  appHasUpdate,
  appReleaseNotes,
  appVersion,
  initAppState,
} = useAppState()

const { mennoRefreshing, mennoIsAutoRefresh, mennoProgress, checkRefreshNeeded, refresh, load } = useMennoData()

const tabList = ['Ledger', 'Mission Data', 'Lifetime Data', 'Settings', 'About']

const mounted = ref(false)
const updateModalDismissed = ref(false)

onMounted(async () => {
  await initAppState()
  registerShortcuts()
  mounted.value = true

  // Auto-refresh Menno data if needed - runs after UI is visible
  if (await checkRefreshNeeded()) {
    await refresh(true)
  }
  await load()
})
</script>
