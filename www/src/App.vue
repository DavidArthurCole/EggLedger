<template>
  <div v-show="mounted" class="h-stretch flex flex-col space-y-3 pb-3 bg-darker">
    <TabBar :tabs="tabList" v-model:active-tab="activeTab" />

    <SettingsView v-show="activeTab === 'Settings'" />
    <LedgerView v-show="activeTab === 'Ledger'" />
    <MissionDataView v-show="activeTab === 'Mission Data'" />
    <LifetimeDataView v-show="activeTab === 'Lifetime Data'" />
    <AboutView v-show="activeTab === 'About'" />

    <UpdateModal
      :visible="!!appHasUpdate && !updateModalDismissed"
      :release-tag="appHasUpdate"
      :release-notes="appReleaseNotes"
      @close="updateModalDismissed = true"
    />
    <MennoLoadingModal
      :visible="mennoRefreshing"
      :is-auto-refresh="mennoIsAutoRefresh"
      :progress="mennoProgress"
    />
  </div>
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
  initAppState,
} = useAppState()

const { mennoRefreshing, mennoIsAutoRefresh, mennoProgress, checkRefreshNeeded, refresh, load } = useMennoData()

const tabList = ['Ledger', 'Mission Data', 'Lifetime Data', 'Settings', 'About']
const mounted = ref(false)
const updateModalDismissed = ref(false)

onMounted(async () => {
  await initAppState()
  registerShortcuts()

  // Auto-refresh Menno data if needed
  if (await checkRefreshNeeded()) {
    await refresh(true)
  }
  await load()

  mounted.value = true
})
</script>
