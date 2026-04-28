import { ref } from 'vue'
import type { PossibleArtifact, PossibleMission, PossibleTarget } from '../types/bridge'

const artifactConfigs = ref<PossibleArtifact[]>([])
const maxQuality = ref(0)
const durationConfigs = ref<PossibleMission[]>([])
const possibleTargets = ref<PossibleTarget[]>([])
let _loaded = false
let _loading: Promise<void> | null = null

export function useSharedConfigs() {
  async function loadSharedConfigs(): Promise<void> {
    if (_loaded) return
    if (_loading) return _loading
    _loading = (async () => {
      ;[artifactConfigs.value, maxQuality.value, durationConfigs.value, possibleTargets.value] = await Promise.all([
        globalThis.getAfxConfigs(),
        globalThis.getMaxQuality(),
        globalThis.getDurationConfigs(),
        globalThis.getPossibleTargets(),
      ])
      _loaded = true
    })()
    return _loading
  }

  return { artifactConfigs, maxQuality, durationConfigs, possibleTargets, loadSharedConfigs }
}
