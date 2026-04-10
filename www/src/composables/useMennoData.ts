import { ref, computed } from 'vue'
import type { ConfigurationItem } from '../types/bridge'

const secondsSinceLastUpdate = ref(Number.MAX_SAFE_INTEGER)
const mennoDataLoaded = ref(false)
const mennoRefreshing = ref(false)
const mennoIsAutoRefresh = ref(false)

async function getMennoData(ship: number, duration: number, level: number, target: number): Promise<ConfigurationItem[]> {
  return globalThis.getMennoData(ship, duration, level, target)
}

export function useMennoData() {
  const lastUpdateString = computed(() => {
    if (secondsSinceLastUpdate.value >= 2147483647) return 'Never'
    const d = new Date(Date.now() - secondsSinceLastUpdate.value * 1000)
    return d.toLocaleString()
  })

  async function checkRefreshNeeded(): Promise<boolean> {
    secondsSinceLastUpdate.value = await globalThis.secondsSinceLastMennoUpdate()
    return globalThis.isMennoRefreshNeeded()
  }

  async function refresh(isAuto = false): Promise<boolean> {
    mennoIsAutoRefresh.value = isAuto
    mennoRefreshing.value = true
    const ok = await globalThis.updateMennoData()
    mennoRefreshing.value = false
    mennoIsAutoRefresh.value = false
    if (ok) secondsSinceLastUpdate.value = 0
    return ok
  }

  async function load(): Promise<boolean> {
    const ok = await globalThis.loadMennoData()
    mennoDataLoaded.value = ok
    return ok
  }

  return {
    secondsSinceLastUpdate, mennoDataLoaded, mennoRefreshing, mennoIsAutoRefresh,
    lastUpdateString,
    checkRefreshNeeded, refresh, load, getMennoData,
  }
}
