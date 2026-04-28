import { ref, readonly } from 'vue'
import type { Account, DatabaseAccount, ProcessSnapshot } from '../types/bridge'
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
const apiVersionIsStale = ref(false)
const compiledApiVersion = ref('')
const appState = ref<AppState | ''>('')
const logMessages = ref<{ message: string; isError: boolean; timestamp: number }[]>([])
const exportedFiles = ref<string[]>([])
const processLogs = ref<ProcessSnapshot[]>([])

export function useAppState() {
  async function initAppState() {
    const [
      version,
      directory,
      forbidden,
      translocated,
      accounts,
      data,
      [hasUpdate, releaseNotes],
      stale,
      apiVer,
    ] = await Promise.all([
      globalThis.appVersion(),
      globalThis.appDirectory(),
      globalThis.appIsInForbiddenDirectory(),
      globalThis.appIsTranslocated(),
      globalThis.knownAccounts(),
      globalThis.getExistingData(),
      globalThis.checkForUpdates(),
      globalThis.isApiVersionStale(),
      globalThis.getCompiledApiVersion(),
    ])

    appVersion.value = version
    appDirectory.value = directory
    appIsInForbiddenDirectory.value = forbidden
    appIsTranslocated.value = translocated
    knownAccounts.value = accounts ?? []
    existingData.value = data
    appHasUpdate.value = hasUpdate
    appReleaseNotes.value = releaseNotes
    apiVersionIsStale.value = stale
    compiledApiVersion.value = apiVer

    // Register Go-to-JS callbacks
    globalThis.updateKnownAccounts = (accounts) => { knownAccounts.value = accounts ?? [] }
    globalThis.updateState = (state) => {
      appState.value = state as AppState
      if (state === AppState.Success) {
        void globalThis.getExistingData().then((data) => { existingData.value = data })
      }
    }
    globalThis.updateMissionProgress = () => {}   // overridden in useFetch
    globalThis.updateExportedFiles = (files) => { exportedFiles.value = files }
    globalThis.emitMessage = (message, isError) => {
      logMessages.value.push({ message, isError, timestamp: Date.now() })
    }
    globalThis.updateProcesses = (processes) => {
      let pruned = processes
      if (pruned.length > 5) {
        pruned = pruned.filter((p) => p.status !== 'done')
      }
      processLogs.value = pruned
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
    apiVersionIsStale: readonly(apiVersionIsStale),
    compiledApiVersion: readonly(compiledApiVersion),
    appState: readonly(appState),
    logMessages: readonly(logMessages),
    exportedFiles: readonly(exportedFiles),
    processLogs: readonly(processLogs),
    initAppState,
  }
}
