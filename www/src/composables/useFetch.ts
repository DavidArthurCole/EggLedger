import { ref } from 'vue'
import type { MissionProgress } from '../types/bridge'

const isFetching = ref(false)
const progress = ref<MissionProgress | null>(null)

async function stopFetching() {
  await globalThis.stopFetchingPlayerData()
}

export function useFetch() {
  async function fetchPlayerData(playerId: string) {
    isFetching.value = true
    globalThis.updateMissionProgress = (p) => { progress.value = p }
    await globalThis.fetchPlayerData(playerId)
    isFetching.value = false
    progress.value = null
  }

  return { isFetching, progress, fetchPlayerData, stopFetching }
}
