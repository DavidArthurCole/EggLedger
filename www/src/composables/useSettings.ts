import { ref, watch } from 'vue'

const resolutionX = ref(0)
const resolutionY = ref(0)
const scalingFactor = ref(1)
const startInFullscreen = ref(false)
const preferredBrowser = ref<string | null>(null)
const loadedBrowser = ref<string | null>(null)
const allBrowsers = ref<string[]>([])
const autoRefreshMenno = ref(false)
const autoRetry = ref(false)
const hideTimeoutErrors = ref(false)
const workerCount = ref(1)
export const screenshotSafety = ref(false)
export const showMissionProgress = ref(true)
export const collapseOlderSections = ref(true)
export const advancedDropFilter = ref(false)
export const autoExportCsv = ref(true)
export const autoExportXlsx = ref(true)

let _loadingSettings = false

export function maskEid(s: string): string {
  if (!screenshotSafety.value) return s
  return s.replaceAll(/EI\d{16}/g, 'EI[eid-bar]')
}

export function useSettings() {
  async function loadSettings() {
    _loadingSettings = true
    const res = await globalThis.getDefaultResolution()
    resolutionX.value = res[0]
    resolutionY.value = res[1]
    scalingFactor.value = await globalThis.getDefaultScalingFactor()
    startInFullscreen.value = await globalThis.getStartInFullscreen()
    preferredBrowser.value = await globalThis.getPreferredBrowser()
    loadedBrowser.value = await globalThis.getLoadedBrowser()
    autoRefreshMenno.value = await globalThis.getAutoRefreshMennoPreference()
    autoRetry.value = await globalThis.getAutoRetryPreference()
    hideTimeoutErrors.value = await globalThis.getHideTimeoutErrors()
    workerCount.value = await globalThis.getWorkerCount()
    screenshotSafety.value = await globalThis.getScreenshotSafety()
    showMissionProgress.value = await globalThis.getShowMissionProgress()
    collapseOlderSections.value = await globalThis.getCollapseOlderSections()
    advancedDropFilter.value = await globalThis.getAdvancedDropFilter()
    autoExportCsv.value = await globalThis.getAutoExportCsv()
    autoExportXlsx.value = await globalThis.getAutoExportXlsx()
    _loadingSettings = false
  }

  watch(resolutionX, () => { if (!_loadingSettings) globalThis.setDefaultResolution(resolutionX.value, resolutionY.value) })
  watch(resolutionY, () => { if (!_loadingSettings) globalThis.setDefaultResolution(resolutionX.value, resolutionY.value) })
  watch(scalingFactor, () => { if (!_loadingSettings) globalThis.setDefaultScalingFactor(scalingFactor.value) })
  watch(startInFullscreen, () => { if (!_loadingSettings) globalThis.setStartInFullscreen(startInFullscreen.value) })
  watch(autoRefreshMenno, () => { if (!_loadingSettings) globalThis.setAutoRefreshMennoPreference(autoRefreshMenno.value) })
  watch(autoRetry, () => { if (!_loadingSettings) globalThis.setAutoRetryPreference(autoRetry.value) })
  watch(hideTimeoutErrors, () => { if (!_loadingSettings) globalThis.setHideTimeoutErrors(hideTimeoutErrors.value) })
  watch(workerCount, () => { if (!_loadingSettings) globalThis.setWorkerCount(workerCount.value) })
  watch(screenshotSafety, () => { if (!_loadingSettings) globalThis.setScreenshotSafety(screenshotSafety.value) })
  watch(showMissionProgress, () => { if (!_loadingSettings) globalThis.setShowMissionProgress(showMissionProgress.value) })
  watch(collapseOlderSections, () => { if (!_loadingSettings) globalThis.setCollapseOlderSections(collapseOlderSections.value) })
  watch(advancedDropFilter, () => { if (!_loadingSettings) globalThis.setAdvancedDropFilter(advancedDropFilter.value) })
  watch(autoExportCsv, () => { if (!_loadingSettings) globalThis.setAutoExportCsv(autoExportCsv.value) })
  watch(autoExportXlsx, () => { if (!_loadingSettings) globalThis.setAutoExportXlsx(autoExportXlsx.value) })

  async function setPreferredBrowser(path: string) {
    if (await globalThis.setPreferredBrowser(path)) {
      preferredBrowser.value = path
    }
  }

  async function refreshBrowserList() {
    allBrowsers.value = await globalThis.getDetectedBrowsers()
  }

  return {
    resolutionX, resolutionY, scalingFactor, startInFullscreen,
    preferredBrowser, loadedBrowser, allBrowsers, autoRefreshMenno, autoRetry, hideTimeoutErrors,
    workerCount,
    screenshotSafety,
    showMissionProgress,
    collapseOlderSections,
    advancedDropFilter,
    autoExportCsv,
    autoExportXlsx,
    loadSettings, setPreferredBrowser, refreshBrowserList,
  }
}
