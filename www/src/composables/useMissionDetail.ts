import { ref } from 'vue'
import type { Ref } from 'vue'
import type { ConfigurationItem, DatabaseMission, MissionDrop } from '../types/bridge'
import {
  type DropLike,
  sortGroupAlreadyCombed,
  sortedGroupedSpecType,
  inventoryVisualizerSort,
} from './useMissionSorting'
import type { ViewMissionData, InnerDrop, MennoConfigItem } from '../types/missionView'

interface UseMissionDetailDeps {
  isFetching: Ref<boolean>
  loadedEid: Ref<string | null>
  allLoadedMissions: Ref<DatabaseMission[] | null>
  filteredMissions: Ref<DatabaseMission[] | null>
  viewMissionSortMethod: Ref<'default' | 'iv'>
  multiViewMode: Ref<'off' | 'row' | 'free'>
  groupedMissions: Ref<DatabaseMission[][][][]>
  ledgerDate: (unixSeconds: number) => Date
  getMennoData: (
    ship: number,
    duration: number,
    level: number,
    target: number,
  ) => Promise<ConfigurationItem[]>
}

/**
 * Mission-detail overlay state and the load/view handlers for the Mission Data
 * tab. Owns the single- and multi-mission overlay refs plus the functions that
 * open, close, and populate them. All external reactive inputs (the loaded
 * mission lists, the active view-sort/multi-view preferences, grouping output,
 * fetch flag, and the menno/ledger-date helpers) are injected by the caller so
 * the overlay state and the functions that drive it stay co-located.
 */
export function useMissionDetail(deps: UseMissionDetailDeps) {
  const {
    isFetching,
    loadedEid,
    allLoadedMissions,
    filteredMissions,
    viewMissionSortMethod,
    multiViewMode,
    groupedMissions,
    ledgerDate,
    getMennoData,
  } = deps

  const viewMissionData = ref<ViewMissionData | null>(null)
  const missionBeingViewed = ref<string | null>(null)
  const boolMissionBeingViewed = ref(false)

  const multiViewMissionData = ref<ViewMissionData[]>([])
  const missionsBeingViewed = ref<string[]>([])
  const rowViewBeingLoaded = ref(false)
  const dropCachePreloading = ref(false)
  const multiViewFreeSelectIds = ref<string[]>([])
  const multiMissionOverlayOpen = ref(false)
  const multiViewTotalToLoad = ref(0)

  function closeMissionOverlay() {
    viewMissionData.value = null
    missionBeingViewed.value = null
    boolMissionBeingViewed.value = false
  }
  function openMissionOverlay() {
    boolMissionBeingViewed.value = true
  }

  function closeMultiMissionOverlay() {
    multiMissionOverlayOpen.value = false
    multiViewMissionData.value = []
    missionsBeingViewed.value = []
  }
  function openMultiMissionOverlay() {
    multiMissionOverlayOpen.value = true
  }

  async function getSpecificMissionData(eid: string, missionId: string, extendedInfo: boolean, dropCache: Record<string, MissionDrop[]> | null = null, knownMissionInfo: DatabaseMission | null = null): Promise<ViewMissionData | null> {
    const missionInfo = knownMissionInfo ?? await globalThis.getMissionInfo(eid, missionId)
    if (missionInfo.ship == null || missionInfo.ship < 0 || !missionInfo.missionId) return null
    const allDrops = dropCache?.[missionId] ?? await globalThis.getShipDrops(eid, missionId)
    if (allDrops == null) return null

    const artifacts = allDrops.filter((drop) => drop.specType === 'Artifact') as InnerDrop[]
    const stones = allDrops.filter((drop) => drop.specType === 'Stone') as InnerDrop[]
    const stoneFragments = allDrops.filter((drop) => drop.specType === 'StoneFragment') as InnerDrop[]
    const ingredients = allDrops.filter((drop) => drop.specType === 'Ingredient') as InnerDrop[]

    const fm = filteredMissions.value ?? []
    const shipIndex = extendedInfo ? fm.findIndex((m) => m.missionId === missionId) : -1
    const nominal = missionInfo.nominalCapacity || 1

    const base: ViewMissionData = {
      missionInfo,
      artifacts: sortedGroupedSpecType(artifacts as unknown as DropLike[]) as unknown as InnerDrop[],
      stones: sortedGroupedSpecType(stones as unknown as DropLike[]) as unknown as InnerDrop[],
      stoneFragments: sortedGroupedSpecType(stoneFragments as unknown as DropLike[]) as unknown as InnerDrop[],
      ingredients: sortedGroupedSpecType(ingredients as unknown as DropLike[]) as unknown as InnerDrop[],
      launchDT: ledgerDate(missionInfo.launchDT),
      returnDT: ledgerDate(missionInfo.returnDT),
      durationStr: missionInfo.durationString,
      capacityModifier: Math.min(2, missionInfo.capacity / nominal),
      prevMission: extendedInfo && shipIndex > 0 ? fm[shipIndex - 1].missionId : null,
      nextMission: extendedInfo && shipIndex >= 0 && shipIndex < fm.length - 1 ? fm[shipIndex + 1].missionId : null,
      mennoData: { configs: [], totalDropsCount: 0 },
    }
    return base
  }

  async function viewSpecificMission(missionId: string, returnValues = false, dropCache: Record<string, MissionDrop[]> | null = null, knownMissionInfo: DatabaseMission | null = null, mennoCache: Map<string, MennoConfigItem[]> | null = null): Promise<ViewMissionData | false> {
    if (isFetching.value) return false
    const newMissionViewData = await getSpecificMissionData(loadedEid.value ?? '', missionId, true, dropCache, knownMissionInfo)
    if (newMissionViewData == null) return false
    const sortFn =
      viewMissionSortMethod.value === 'iv' ? inventoryVisualizerSort : sortGroupAlreadyCombed

    const mi = newMissionViewData.missionInfo as (DatabaseMission & { targetInt?: number }) | undefined
    if (mi) {
      const mennoTargetInt = mi.targetInt === -1 ? 10000 : (mi.targetInt ?? 10000)
      const mennoShip = mi.ship
      const mennoDuration = mi.durationType
      const mennoLevel = mi.level
      const mennoKey = `${mennoShip}_${mennoDuration}_${mennoLevel}_${mennoTargetInt}`
      let mennoConfigItems: MennoConfigItem[] | null = null
      if (mennoCache?.has(mennoKey)) {
        mennoConfigItems = mennoCache.get(mennoKey)!
      } else {
        const fetched = await getMennoData(mennoShip, mennoDuration, mennoLevel, mennoTargetInt)
        mennoConfigItems = fetched as unknown as MennoConfigItem[]
        mennoCache?.set(mennoKey, mennoConfigItems)
      }
      if (mennoConfigItems) {
        newMissionViewData.mennoData = {
          totalDropsCount: mennoConfigItems.reduce(
            (acc: number, cur: { totalDrops: number }) => acc + cur.totalDrops,
            0,
          ),
          configs: mennoConfigItems,
        }
      }
    }

    newMissionViewData.artifacts = sortFn((newMissionViewData.artifacts ?? []) as DropLike[]) as unknown as InnerDrop[]
    newMissionViewData.stones = sortFn((newMissionViewData.stones ?? []) as DropLike[]) as unknown as InnerDrop[]
    newMissionViewData.stoneFragments = sortFn(
      (newMissionViewData.stoneFragments ?? []) as DropLike[],
    ) as unknown as InnerDrop[]
    newMissionViewData.ingredients = sortFn((newMissionViewData.ingredients ?? []) as DropLike[]) as unknown as InnerDrop[]

    if (returnValues) return newMissionViewData
    missionBeingViewed.value = missionId
    viewMissionData.value = newMissionViewData
    openMissionOverlay()
    return newMissionViewData
  }

  async function triggerRowView(yearIndex?: number, monthIndex?: number, dayIndex?: number) {
    rowViewBeingLoaded.value = true
    if (
      yearIndex == null &&
      monthIndex == null &&
      dayIndex == null &&
      multiViewMode.value === 'free' &&
      multiViewFreeSelectIds.value.length > 0
    ) {
      viewRowOfMissions(multiViewFreeSelectIds.value)
      return
    }
    if (yearIndex == null || monthIndex == null || dayIndex == null) {
      rowViewBeingLoaded.value = false
      return
    }
    const grouped = groupedMissions.value
    const ids = grouped[yearIndex][monthIndex][dayIndex].map((m) => m.missionId)
    if (ids == null || ids.length === 0) {
      rowViewBeingLoaded.value = false
      return
    }
    if (ids.length === 1) {
      await viewSpecificMission(ids[0])
      rowViewBeingLoaded.value = false
      return
    }
    viewRowOfMissions(ids)
  }

  function handleMultiViewSelection(event: Event, missionId: string) {
    const checked = (event.target as HTMLInputElement | null)?.checked ?? false
    if (checked) multiViewFreeSelectIds.value.push(missionId)
    else multiViewFreeSelectIds.value = multiViewFreeSelectIds.value.filter((id) => id !== missionId)
  }

  async function viewRowOfMissions(missionIds: string[]) {
    if (isFetching.value) {
      rowViewBeingLoaded.value = false
      return
    }
    multiViewTotalToLoad.value = missionIds.length
    openMultiMissionOverlay()
    missionsBeingViewed.value = []
    multiViewMissionData.value = []
    const dropCache: Record<string, MissionDrop[]> | null = null
    const missionInfoLookup: Record<string, DatabaseMission> = {}
    for (const m of allLoadedMissions.value ?? []) missionInfoLookup[m.missionId] = m
    const mennoCache = new Map<string, MennoConfigItem[]>()
    for (const missionId of missionIds) {
      const data = await viewSpecificMission(missionId, true, dropCache, missionInfoLookup[missionId] ?? null, mennoCache)
      if (data !== false) {
        multiViewMissionData.value.push(data)
        missionsBeingViewed.value.push(missionId)
      }
    }
    rowViewBeingLoaded.value = false
  }

  return {
    viewMissionData,
    missionBeingViewed,
    boolMissionBeingViewed,
    multiViewMissionData,
    missionsBeingViewed,
    rowViewBeingLoaded,
    dropCachePreloading,
    multiViewFreeSelectIds,
    multiMissionOverlayOpen,
    multiViewTotalToLoad,
    closeMissionOverlay,
    closeMultiMissionOverlay,
    getSpecificMissionData,
    viewSpecificMission,
    triggerRowView,
    handleMultiViewSelection,
    viewRowOfMissions,
  }
}
