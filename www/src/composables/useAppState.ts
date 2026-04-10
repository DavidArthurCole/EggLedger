import { ref, readonly } from 'vue'
import type { Account, DatabaseAccount } from '../types/bridge'
import { AppState } from '../types/bridge'

const appVersion = ref('')
const appDirectory = ref('')
const appIsInForbiddenDirectory = ref(false)
const appIsTranslocated = ref(false)
const knownAccounts = ref<Account[]>([])
const existingData = ref<DatabaseAccount[]>([])
const activeTab = ref<string>('Ledger')
const appHasUpdate = ref('')
const appReleaseNotes = ref('')
const appState = ref<AppState | ''>('')
const logMessages = ref<{ message: string; isError: boolean }[]>([])
const exportedFiles = ref<string[]>([])

export function useAppState() {
  async function initAppState() {
    appVersion.value = await globalThis.appVersion()
    appDirectory.value = await globalThis.appDirectory()
    appIsInForbiddenDirectory.value = await globalThis.appIsInForbiddenDirectory()
    appIsTranslocated.value = await globalThis.appIsTranslocated()
    knownAccounts.value = await globalThis.knownAccounts()
    existingData.value = await globalThis.getExistingData()

    const [hasUpdate, releaseNotes] = await globalThis.checkForUpdates()
    appHasUpdate.value = hasUpdate
    appReleaseNotes.value = releaseNotes

    // Register Go-to-JS callbacks
    globalThis.updateKnownAccounts = (accounts) => { knownAccounts.value = accounts }
    globalThis.updateState = (state) => { appState.value = state as AppState }
    globalThis.updateMissionProgress = () => {}   // overridden in useFetch
    globalThis.updateExportedFiles = (files) => { exportedFiles.value = files }
    globalThis.emitMessage = (message, isError) => {
      logMessages.value.push({ message, isError })
    }
  }

  return {
    appVersion: readonly(appVersion),
    appDirectory: readonly(appDirectory),
    appIsInForbiddenDirectory: readonly(appIsInForbiddenDirectory),
    appIsTranslocated: readonly(appIsTranslocated),
    knownAccounts: readonly(knownAccounts),
    existingData,
    activeTab,
    appHasUpdate: readonly(appHasUpdate),
    appReleaseNotes: readonly(appReleaseNotes),
    appState: readonly(appState),
    logMessages: readonly(logMessages),
    exportedFiles: readonly(exportedFiles),
    initAppState,
  }
}
