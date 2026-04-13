import { ref, computed, nextTick } from 'vue'
import type { ConfigurationItem, MennoDownloadProgress } from '../types/bridge'

const secondsSinceLastUpdate = ref(Number.MAX_SAFE_INTEGER)
const mennoDataLoaded = ref(false)
const mennoRefreshing = ref(false)
const mennoIsAutoRefresh = ref(false)
const mennoProgress = ref<MennoDownloadProgress | null>(null)

async function getMennoData(ship: number, duration: number, level: number, target: number): Promise<ConfigurationItem[]> {
  return globalThis.getMennoData(ship, duration, level, target)
}

export function useMennoData() {
  const lastUpdateString = computed(() => {
    if (secondsSinceLastUpdate.value >= 2147483647) return 'Never'
    const d = new Date(Date.now() - secondsSinceLastUpdate.value * 1000)
    return d.toLocaleString()
  })

  const nextWeeklyRefreshString = computed(() => {
    if (secondsSinceLastUpdate.value >= 2147483647) return null
    const nextAt = new Date(Date.now() - secondsSinceLastUpdate.value * 1000 + 7 * 24 * 3600 * 1000)
    return nextAt.toLocaleString()
  })

  async function checkRefreshNeeded(): Promise<boolean> {
    secondsSinceLastUpdate.value = await globalThis.secondsSinceLastMennoUpdate()
    return globalThis.isMennoRefreshNeeded()
  }

  async function refresh(isAuto = false): Promise<boolean> {
    mennoIsAutoRefresh.value = isAuto
    mennoRefreshing.value = true
    // Wait for Vue to paint the modal before starting, so the goroutine's
    // first ui.Eval doesn't race with lorca's binding-resolution write.
    await nextTick()
    const minDisplay = new Promise<void>((r) => setTimeout(r, 1500))
    const download = new Promise<boolean>((resolve) => {
      globalThis.onMennoRefreshDone = resolve
      globalThis.updateMennoDownloadProgress = (p) => { mennoProgress.value = p }
      globalThis.updateMennoData()
    })
    const [ok] = await Promise.all([download, minDisplay])
    globalThis.updateMennoDownloadProgress = () => {}
    globalThis.onMennoRefreshDone = () => {}
    mennoRefreshing.value = false
    mennoIsAutoRefresh.value = false
    mennoProgress.value = null
    if (ok) secondsSinceLastUpdate.value = 0
    return ok
  }

  async function load(): Promise<boolean> {
    const ok = await globalThis.loadMennoData()
    mennoDataLoaded.value = ok
    return ok
  }

  return {
    secondsSinceLastUpdate, mennoDataLoaded, mennoRefreshing, mennoIsAutoRefresh, mennoProgress,
    lastUpdateString, nextWeeklyRefreshString,
    checkRefreshNeeded, refresh, load, getMennoData,
  }
}
