import { ref, watch } from 'vue'

const resolutionX = ref(0)
const resolutionY = ref(0)
const scalingFactor = ref(1)
const startInFullscreen = ref(false)
const preferredBrowser = ref<string | null>(null)
const allBrowsers = ref<string[]>([])
const autoRefreshMenno = ref(false)
const autoRetry = ref(false)
const hideTimeoutErrors = ref(false)
const defaultViewMode = ref('default')
const workerCount = ref(1)
const screenshotSafety = ref(false)

export function maskEid(s: string): string {
  if (!screenshotSafety.value) return s
  return s.replaceAll(/EI\d{16}/g, 'EI\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022')
}

export function useSettings() {
  async function loadSettings() {
    const res = await globalThis.getDefaultResolution()
    resolutionX.value = res[0]
    resolutionY.value = res[1]
    scalingFactor.value = await globalThis.getDefaultScalingFactor()
    startInFullscreen.value = await globalThis.getStartInFullscreen()
    preferredBrowser.value = await globalThis.getPreferredBrowser()
    autoRefreshMenno.value = await globalThis.getAutoRefreshMennoPreference()
    autoRetry.value = await globalThis.getAutoRetryPreference()
    hideTimeoutErrors.value = await globalThis.getHideTimeoutErrors()
    defaultViewMode.value = await globalThis.getDefaultViewMode()
    workerCount.value = await globalThis.getWorkerCount()
    screenshotSafety.value = await globalThis.getScreenshotSafety()
  }

  watch(resolutionX, () => globalThis.setDefaultResolution(resolutionX.value, resolutionY.value))
  watch(resolutionY, () => globalThis.setDefaultResolution(resolutionX.value, resolutionY.value))
  watch(scalingFactor, () => globalThis.setDefaultScalingFactor(scalingFactor.value))
  watch(startInFullscreen, () => globalThis.setStartInFullscreen(startInFullscreen.value))
  watch(autoRefreshMenno, () => globalThis.setAutoRefreshMennoPreference(autoRefreshMenno.value))
  watch(autoRetry, () => globalThis.setAutoRetryPreference(autoRetry.value))
  watch(hideTimeoutErrors, () => globalThis.setHideTimeoutErrors(hideTimeoutErrors.value))
  watch(defaultViewMode, () => globalThis.setDefaultViewMode(defaultViewMode.value))
  watch(workerCount, () => globalThis.setWorkerCount(workerCount.value))
  watch(screenshotSafety, () => globalThis.setScreenshotSafety(screenshotSafety.value))

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
    preferredBrowser, allBrowsers, autoRefreshMenno, autoRetry, hideTimeoutErrors, defaultViewMode,
    workerCount,
    screenshotSafety,
    loadSettings, setPreferredBrowser, refreshBrowserList,
  }
}
