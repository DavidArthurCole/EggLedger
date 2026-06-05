import { ref, computed, watch } from 'vue'
import type { Ref } from 'vue'
import type { DatabaseMission, MissionDrop } from '../types/bridge'
import type { LedgerData } from '../components/DropDisplayContainer.vue'

export interface DropLike {
  id: number
  name: string
  level: number
  rarity: number
  quality: number
  ivOrder: number
  specType: string
  count?: number
}

export interface LifetimeDrop extends MissionDrop {
  count: number
  missionInfos?: DatabaseMission[]
  digShipInfo?: boolean
}

// LifetimeData matches LedgerData (for DropDisplayContainer)
export interface LifetimeData extends LedgerData {
  missionCount: number
  artifacts: LifetimeDrop[]
  stones: LifetimeDrop[]
  stoneFragments: LifetimeDrop[]
  ingredients: LifetimeDrop[]
}

function sortGroupAlreadyCombed<T extends DropLike>(collection: T[]): T[] {
  return collection
    .sort((a, b) => {
      if (a.level > b.level) return -1
      if (a.level < b.level) return 1
      if (a.rarity > b.rarity) return -1
      if (a.rarity < b.rarity) return 1
      if (a.id > b.id) return -1
      if (a.id < b.id) return 1
      if (a.quality < b.quality) return -1
      if (a.quality > b.quality) return 1
      return 0
    })
    .reverse()
}

function inventoryVisualizerSort<T extends DropLike>(collection: T[]): T[] {
  return collection.sort((a, b) => {
    if (a.rarity > b.rarity) return -1
    if (a.rarity < b.rarity) return 1
    if (a.ivOrder > b.ivOrder) return -1
    if (a.ivOrder < b.ivOrder) return 1
    if (a.level > b.level) return -1
    if (a.level < b.level) return 1
    return 0
  })
}

function sortGroupByCount<T extends DropLike>(collection: T[]): T[] {
  return collection.sort((a, b) => {
    const ac = a.count ?? 0
    const bc = b.count ?? 0
    if (ac > bc) return -1
    if (ac < bc) return 1
    if (a.level > b.level) return -1
    if (a.level < b.level) return 1
    if (a.rarity > b.rarity) return -1
    if (a.rarity < b.rarity) return 1
    if (a.id > b.id) return -1
    if (a.id < b.id) return 1
    if (a.quality < b.quality) return -1
    if (a.quality > b.quality) return 1
    return 0
  })
}

function shuffle<T>(collection: T[]): T[] {
  return collection.sort(() => Math.random() - 0.5)
}

/**
 * Sort options and the reSortLifetime transform for the Lifetime Data tab.
 * The caller owns the lifetimeData ref and passes it in; reSortLifetime mutates
 * it in place. settingsLoaded gates the globalThis preference writes so the
 * onMounted hydration does not echo back to the backend.
 */
export function useLifetimeSorting(
  lifetimeData: Ref<LifetimeData | null>,
  mennoDataLoaded: Ref<boolean>,
  settingsLoaded: Ref<boolean>,
) {
  const lifetimeSortMethod = ref<'default' | 'iv' | 'count' | 'random'>('default')
  const lifetimeShowDropsPerShip = ref(false)
  const lifetimeAllowExpectedTotals = ref(false)
  const lifetimeShowExpectedTotalsPref = ref(false)
  const lifetimeShowExpectedTotals = computed(
    () => lifetimeAllowExpectedTotals.value && lifetimeShowExpectedTotalsPref.value,
  )

  function reSortLifetime() {
    const ld = lifetimeData.value
    if (ld == null) return
    const sortFn = (() => {
      switch (lifetimeSortMethod.value) {
        case 'default':
          return sortGroupAlreadyCombed
        case 'iv':
          return inventoryVisualizerSort
        case 'count':
          return sortGroupByCount
        case 'random':
          return shuffle
        default:
          return sortGroupAlreadyCombed
      }
    })()
    lifetimeData.value = {
      ...ld,
      artifacts: sortFn([...ld.artifacts] as unknown as DropLike[]) as unknown as LifetimeDrop[],
      stones: sortFn([...ld.stones] as unknown as DropLike[]) as unknown as LifetimeDrop[],
      stoneFragments: sortFn([...ld.stoneFragments] as unknown as DropLike[]) as unknown as LifetimeDrop[],
      ingredients: sortFn([...ld.ingredients] as unknown as DropLike[]) as unknown as LifetimeDrop[],
    }
  }

  watch(lifetimeSortMethod, (val) => {
    reSortLifetime()
    if (settingsLoaded.value) globalThis.setLifetimeSortMethod(val)
  })
  watch(lifetimeShowDropsPerShip, (val) => { if (settingsLoaded.value) globalThis.setLifetimeShowDropsPerShip(val) })
  watch(lifetimeShowExpectedTotalsPref, (val) => { if (settingsLoaded.value) globalThis.setLifetimeShowExpectedTotals(val) })
  watch(mennoDataLoaded, () => {
    if (!mennoDataLoaded.value) lifetimeShowExpectedTotalsPref.value = false
  })

  return {
    lifetimeSortMethod,
    lifetimeShowDropsPerShip,
    lifetimeAllowExpectedTotals,
    lifetimeShowExpectedTotalsPref,
    lifetimeShowExpectedTotals,
    reSortLifetime,
  }
}
