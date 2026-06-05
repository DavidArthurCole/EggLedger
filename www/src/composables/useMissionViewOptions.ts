import { ref, computed, watch } from 'vue'
import type { Ref } from 'vue'
import type { DatabaseMission } from '../types/bridge'

/**
 * Mission-list display preference state and the mission-type tab filtering for
 * the Mission Data tab. Owns the view-option refs (which the OptionsPanel binds
 * via v-model) plus the missionTypeTab state and its derived computeds.
 *
 * allLoadedMissions drives hasBothMissionTypes; filteredMissions drives
 * tabFilteredMissions - both source refs are passed in by the caller so the
 * computeds stay co-located with the state they react to. settingsLoaded gates
 * the globalThis preference writes so the onMounted hydration does not echo back
 * to the backend.
 */
export function useMissionViewOptions(
  allLoadedMissions: Ref<DatabaseMission[] | null>,
  filteredMissions: Ref<DatabaseMission[] | null>,
  mennoDataLoaded: Ref<boolean>,
  settingsLoaded: Ref<boolean>,
) {
  const viewByDate = ref(false)
  const viewMissionTimes = ref(true)
  const recolorDC = ref(false)
  const recolorBC = ref(false)
  const showExpectedDropsPerShip = ref(true)
  const multiViewMode = ref<'off' | 'row' | 'free'>('off')
  const viewMissionSortMethod = ref<'default' | 'iv'>('default')

  /** null=All, 0=Home, 1=Virtue */
  const missionTypeTab = ref<number | null>(null)

  const hasBothMissionTypes = computed(() => {
    const missions = allLoadedMissions.value
    if (!missions || missions.length === 0) return false
    return missions.some(m => m.missionType === 0) && missions.some(m => m.missionType === 1)
  })

  watch(hasBothMissionTypes, (val) => {
    if (!val) missionTypeTab.value = null
  })

  const tabFilteredMissions = computed(() => {
    if (missionTypeTab.value === null || filteredMissions.value === null) return filteredMissions.value
    return filteredMissions.value.filter((m) => m.missionType === missionTypeTab.value)
  })

  watch(mennoDataLoaded, () => {
    if (!mennoDataLoaded.value) showExpectedDropsPerShip.value = false
  })

  watch(viewByDate, (val) => { if (settingsLoaded.value) globalThis.setMissionViewByDate(val) })
  watch(viewMissionTimes, (val) => { if (settingsLoaded.value) globalThis.setMissionViewTimes(val) })
  watch(recolorDC, (val) => { if (settingsLoaded.value) globalThis.setMissionRecolorDC(val) })
  watch(recolorBC, (val) => { if (settingsLoaded.value) globalThis.setMissionRecolorBC(val) })
  watch(showExpectedDropsPerShip, (val) => { if (settingsLoaded.value) globalThis.setMissionShowExpectedDrops(val) })
  watch(multiViewMode, (val) => { if (settingsLoaded.value) globalThis.setMissionMultiViewMode(val) })
  watch(viewMissionSortMethod, (val) => { if (settingsLoaded.value) globalThis.setMissionSortMethod(val) })

  return {
    viewByDate,
    viewMissionTimes,
    recolorDC,
    recolorBC,
    showExpectedDropsPerShip,
    multiViewMode,
    viewMissionSortMethod,
    missionTypeTab,
    hasBothMissionTypes,
    tabFilteredMissions,
  }
}
