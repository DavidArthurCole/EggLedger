<template>
  <div class="view-layout overflow-hidden">
    <!-- Target filter overlay -->
    <SearchOverSelector
      v-if="targetFilterMenuOpen"
      :item-list="targetSelectList"
      ledger-type="target"
      :is-lifetime="true"
      @close="closeTargetFilterMenu"
      @select="selectTargetFilter"
      @input="(val: string) => (targetSearchTerm = val)"
    />

    <!-- Drop filter overlay -->
    <SearchOverSelector
      v-if="dropFilterMenuOpen"
      :item-list="dropSelectList"
      ledger-type="drop"
      :is-lifetime="true"
      @close="closeDropFilterMenu"
      @select="selectDropFilter"
      @input="(val: string) => (dropSearchTerm = val)"
    />


    <!-- Lifetime progress -->
    <SegmentedProgressBar
      v-if="doesDataExist"
      :active="isProgressSpinning"
      :segments="lifetimeProgressSegments"
      :status-text="statusText"
      :status-class="statusColor"
      :is-spinning="isProgressSpinning"
    />

    <!-- Filter panel -->
    <div
      v-if="doesDataExist && !lifetimeDataBeingLoaded"
      class="filter-panel"
    >
      <span
        class="h-20 font-bold text-gray-400"
      >Mission Filter: </span>
      <button
        id="toggleLifetimeFilterButton"
        class="text-base toggle-link"
        type="button"
        @click="(e) => { e.preventDefault(); hideLifetimeFilter = !hideLifetimeFilter }"
      >
        {{ hideLifetimeFilter ? 'Show' : 'Hide' }}
      </button>
      <form
        v-if="!hideLifetimeFilter || lifetimeDataBeingFiltered"
        id="lifetimeFilterForm"
        name="lifetimeFilterForm"
        class="filter-form text-xs"
        @submit="onFilterSubmit"
      >
        <FullFilter
          :filter-array="generateFilterConditionsArr()"
          :mod-vals="filterModVals()"
          :is-lifetime="true"
          :get-filter-value-options="getFilterValueOptions"
          @handle-filter-change="handleFilterChange"
          @handle-or-filter-change="handleOrFilterChange"
          @remove-and-shift="removeAndShift"
          @remove-or-and-shift="removeOrAndShift"
          @open-target-filter-menu="openTargetFilterMenu"
          @open-drop-filter-menu="openDropFilterMenu"
          @add-or="addOr"
          @change-filter-value="changeFilterValue"
        />
        <hr class="mt-1rem w-full" />
        <button
          v-if="lifetimeData != null"
          id="lifetime-filter-apply-button"
          type="submit"
          class="apply-filter-button"
          :disabled="lifetimeDataBeingFiltered || !filterHasChanged"
        >
          Apply Filter
        </button>
      </form>
    </div>

    <!-- Options panel -->
    <div
      v-if="doesDataExist && lifetimeData != null && !lifetimeDataBeingLoaded"
      class="min-h-7 max-h-50 px-2 py-2 text-sm text-gray-400 bg-darkest rounded-md tabular-nums overflow-auto mt-0_75rem"
    >
      <div>
        <span class="mr-0_5rem section-heading">Options</span>
        <button
          id="toggleLifetimeOptionsButton"
          class="text-base toggle-link"
          type="button"
          @click="hideLifetimeOptions = !hideLifetimeOptions"
        >
          {{ hideLifetimeOptions ? 'Show' : 'Hide' }}
        </button>
      </div>
      <div v-if="!hideLifetimeOptions">
        <div>
          <span class="section-heading">Sort Method</span><br />
          <span class="opt-span">
            <label for="lifetimeSortDefault" class="ext-opt-label">Default</label>
            <input id="lifetimeSortDefault" v-model="lifetimeSortMethod" type="radio" value="default" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="lifetimeSortIV" class="ext-opt-label">Inventory Visualizer</label>
            <input id="lifetimeSortIV" v-model="lifetimeSortMethod" type="radio" value="iv" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="lifetimeSortTotalCount" class="ext-opt-label">Total Count</label>
            <input id="lifetimeSortTotalCount" v-model="lifetimeSortMethod" type="radio" value="count" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="lifetimeSortRandom" class="ext-opt-label">Random</label>
            <input id="lifetimeSortRandom" v-model="lifetimeSortMethod" type="radio" value="random" class="ext-opt-check" />
          </span>
        </div>
        <div class="mt-0_5rem">
          <span class="section-heading">Display</span><br />
          <span class="opt-span">
            <label for="lifetimeDataPerShip" class="ext-opt-label">Show 'Average Drops per Ship'</label>
            <input id="lifetimeDataPerShip" v-model="lifetimeShowDropsPerShip" type="checkbox" class="ext-opt-check" />
          </span>
          <span v-if="lifetimeAllowExpectedTotals" class="opt-span">
            <label for="lifetimeExpectedTotals" class="ext-opt-label">Show 'Expected Total Drops'</label>
            <input
              id="lifetimeExpectedTotals"
              v-model="lifetimeShowExpectedTotalsPref"
              type="checkbox"
              :disabled="!mennoDataLoaded"
              class="ext-opt-check"
            />
          </span>
        </div>
      </div>
    </div>

    <!-- Load button (shown before first load) -->
    <div
      v-if="doesDataExist && lifetimeData == null && !lifetimeDataBeingLoaded"
      class="flex items-center justify-center mt-4"
    >
      <button
        type="button"
        class="px-6 py-2 text-base font-semibold text-white bg-blue-600 rounded-md hover:bg-blue-500 active:bg-blue-700 disabled:opacity-50"
        :disabled="!activeAccountId"
        @click="void viewLifetimeDataOfEid(true)"
      >
        Load Lifetime Data
      </button>
    </div>

    <!-- Data display -->
    <div
      v-if="doesDataExist"
      class="flex-1 min-h-0 px-2 py-1 overflow-auto shadow-sm block text-xs font-mono text-gray-300 bg-darkest rounded-md text-center"
    >
      <div
        v-if="(lifetimeData != null && !lifetimeDataBeingLoaded) || lifetimeDataBeingFiltered"
        id="lifetime-ss"
        class="mt-4"
      >
        <span
          v-if="!lifetimeDataBeingFiltered && lifetimeData != null"
          class="ledger-underline text-base text-gray-400"
        >
          Data compiled from {{ lifetimeData.missionCount }} Missions
          <span v-if="lifetimeDataExcludedCount !== 0">({{ lifetimeDataExcludedCount }} Filtered Out)</span>
        </span>
        <DropDisplayContainer
          v-if="!lifetimeDataBeingFiltered && lifetimeData != null && !lifetimeDataBeingLoaded"
          :data="lifetimeData"
          ledger-type="lifetime"
          :lifetime-show-per-ship="lifetimeShowDropsPerShip"
          :show-expected-drops="lifetimeShowExpectedTotals"
        />
        <div v-else class="max-w-full items-center">
          <hr class="mt-1rem mb-1rem w-full" />
          <span class="mt-0_5rem p-0_5rem text-lg">Lifetime data is being compiled, please wait...</span><br />
          <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
        </div>
      </div>
      <!-- Loading screen for the initial compile (no data yet). -->
      <div
        v-else-if="lifetimeDataBeingLoaded"
        class="mt-8 flex flex-col items-center justify-center gap-2"
      >
        <span class="p-0_5rem text-lg">Lifetime data is being compiled, please wait...</span>
        <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
      </div>
    </div>

    <!-- No data fallback -->
    <NoDataFallback v-if="!doesDataExist" @navigate="activeTab = $event" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useAppState } from '../composables/useAppState'
import { useMennoData } from '../composables/useMennoData'
import { useFilters } from '../composables/useFilters'
import { useActiveAccount } from '../composables/useActiveAccount'
import { useSharedConfigs } from '../composables/useSharedConfigs'
import { useLifetimeSorting, type LifetimeData, type LifetimeDrop } from '../composables/useLifetimeSorting'
import { useLifetimeProgress, LifetimeLoadState } from '../composables/useLifetimeProgress'
import type {
  DatabaseMission,
  MissionDrop,
} from '../types/bridge'
import { AppState } from '../types/bridge'
import FullFilter from '../components/FullFilter.vue'
import NoDataFallback from '../components/NoDataFallback.vue'
import SearchOverSelector from '../components/SearchOverSelector.vue'
import DropDisplayContainer, { type LedgerData } from '../components/DropDisplayContainer.vue'
import SegmentedProgressBar from '../components/SegmentedProgressBar.vue'

// Shared state

const { existingData, activeTab, appState } = useAppState()
const { mennoDataLoaded, getMennoData, load: loadMennoData } = useMennoData()
const { activeAccountId } = useActiveAccount()
const { artifactConfigs, maxQuality, durationConfigs, possibleTargets, loadSharedConfigs } = useSharedConfigs()

const doesDataExist = computed(() => existingData.value.length > 0)

// Lifetime data ref (owned here; mutated by viewLifetimeDataOfEid and the sort composable)

const lifetimeData = ref<LifetimeData | null>(null)

// Progress and segment-bar machinery

const {
  lifetimeState,
  lifetimeSuccessTime,
  lifetimeDataBeingFiltered,
  lifetimeDataExcludedCount,
  lifetimeDataLoadedProgress,
  lifetimeDataBeingLoaded,
  isProgressSpinning,
  lifetimeProgressSegments,
  statusColor,
  statusText,
} = useLifetimeProgress()

// Sorting options

const settingsLoaded = ref(false)

const {
  lifetimeSortMethod,
  lifetimeShowDropsPerShip,
  lifetimeAllowExpectedTotals,
  lifetimeShowExpectedTotalsPref,
  lifetimeShowExpectedTotals,
  reSortLifetime,
} = useLifetimeSorting(lifetimeData, mennoDataLoaded, settingsLoaded)

// Filter composable
const {
  dataFilter, orDataFilter, filterHasChanged, getFilterValueOptions,
  filterTopLevel, filterOperators, filterValues,
  changeFilterValue, handleFilterChange, handleOrFilterChange,
  addOr, removeAndShift, removeOrAndShift, generateFilterConditionsArr,
  filterModVals, clearFilter, ledgerDate, ledgerDateObj, missionMatchesFilter,
  dropSelectList, dropFilterMenuOpen, dropSearchTerm,
  targetSelectList, targetFilterMenuOpen, targetSearchTerm,
  openDropFilterMenu, closeDropFilterMenu, selectDropFilter,
  openTargetFilterMenu, closeTargetFilterMenu, selectTargetFilter,
} = useFilters({
  idSuffix: 'lifetime',
  accountId: activeAccountId,
  durationConfigs, possibleTargets, maxQuality, artifactConfigs,
})

// View-specific filter state
const hideLifetimeFilter = ref(false)
const hideLifetimeOptions = ref(false)

// "Has exactly one" watchers for Menno data
const lifetimeHasExactlyOneTarget = ref(false)
const lifetimeOneTargetInt = ref<string | null>(null)
const lifetimeHasExactlyOneShip = ref(false)
const lifetimeOneShipInt = ref<string | null>(null)
const lifetimeHasExactlyOneDuration = ref(false)
const lifetimeOneDurationInt = ref<string | null>(null)
const lifetimeHasExactlyOneLevel = ref(false)
const lifetimeOneLevelInt = ref<string | null>(null)

function refreshExactlyOneFlags() {
  const kinds: Array<['target' | 'ship' | 'duration' | 'level', typeof lifetimeHasExactlyOneTarget, typeof lifetimeOneTargetInt]> = [
    ['target', lifetimeHasExactlyOneTarget, lifetimeOneTargetInt],
    ['ship', lifetimeHasExactlyOneShip, lifetimeOneShipInt],
    ['duration', lifetimeHasExactlyOneDuration, lifetimeOneDurationInt],
    ['level', lifetimeHasExactlyOneLevel, lifetimeOneLevelInt],
  ]
  for (const [kind, flagRef, valRef] of kinds) {
    flagRef.value = false
    valRef.value = null
    const matches = filterTopLevel.value.filter((f) => f === kind)
    if (matches.length !== 1) continue
    const idx = filterTopLevel.value.indexOf(kind)
    const op = filterOperators.value[idx]
    const val = filterValues.value[idx]
    if (op == null || val == null || op !== '=') continue
    flagRef.value = true
    valRef.value = val
  }
}
watch(
  [filterTopLevel, filterOperators, filterValues],
  refreshExactlyOneFlags,
  { deep: true },
)

// Lifetime fetch pipeline

// A tracker keeping a dedupe-Map alongside the array of LifetimeDrop.
interface LifetimeBucket {
  arr: LifetimeDrop[]
  index: Map<string, LifetimeDrop>
}

function makeBucket(): LifetimeBucket {
  return { arr: [], index: new Map() }
}

function mergeItems(bucket: LifetimeBucket, item: LifetimeDrop, missionInfo: DatabaseMission | null) {
  const key = `${item.id}_${item.level}_${item.rarity}`
  const existing = bucket.index.get(key)
  if (existing) {
    existing.count += item.count
    if (missionInfo != null) existing.missionInfos?.push(missionInfo)
  } else {
    bucket.index.set(key, item)
    bucket.arr.push(item)
    if (missionInfo != null) {
      item.missionInfos = [missionInfo]
      item.digShipInfo = false
    }
  }
}

async function getSpecificMissionDataForLifetime(
  eid: string,
  missionId: string,
  dropCache: Record<string, MissionDrop[]> | null,
  missionCache: DatabaseMission[] | null,
) {
  const missionInfo = missionCache?.find((m) => m.missionId === missionId)
    ?? await globalThis.getMissionInfo(eid, missionId)
  if (missionInfo.ship == null || missionInfo.ship < 0 || !missionInfo.missionId) return null
  const allDrops = dropCache?.[missionId] ?? await globalThis.getShipDrops(eid, missionId)
  if (allDrops == null) return null

  const toLifetime = (list: MissionDrop[]): LifetimeDrop[] =>
    (list as unknown as LifetimeDrop[]).map((d) => ({ ...d, count: d.count ?? 1 }))

  return {
    missionInfo,
    artifacts: toLifetime(allDrops.filter((d) => d.specType === 'Artifact')),
    stones: toLifetime(allDrops.filter((d) => d.specType === 'Stone')),
    stoneFragments: toLifetime(allDrops.filter((d) => d.specType === 'StoneFragment')),
    ingredients: toLifetime(allDrops.filter((d) => d.specType === 'Ingredient')),
  }
}

function lifetimeHasExactlyOneConfiguration(firstMatches: number[] | null): boolean {
  if (firstMatches == null) return false
  return (
    (lifetimeHasExactlyOneShip.value || firstMatches[0] !== -1) &&
    (lifetimeHasExactlyOneDuration.value || firstMatches[1] !== -1) &&
    (lifetimeHasExactlyOneLevel.value || firstMatches[2] !== -1) &&
    (lifetimeHasExactlyOneTarget.value || firstMatches[3] !== -1) &&
    mennoDataLoaded.value
  )
}

// Monotonic token so an overlapping load (account switch racing a fetch-complete
// refresh) is cancelled at the next checkpoint instead of clobbering the latest
// load's shared state. A stale call bails after each await rather than committing.
let lifetimeLoadToken = 0

// eslint-disable-next-line sonarjs/cognitive-complexity
async function viewLifetimeDataOfEid(filterLoad: boolean) {
  if (!activeAccountId.value) return
  const myToken = ++lifetimeLoadToken
  lifetimeDataBeingFiltered.value = filterLoad

  const start = performance.now()
  lifetimeState.value = LifetimeLoadState.FetchingIds

  // Fetch mission IDs with a 2.5 second timeout
  const idsJson: string[] | null = await (async () => {
    try {
      const missionIdsPromise = globalThis.getMissionIds(activeAccountId.value!)
      const timeoutPromise = new Promise<null>((resolve) => {
        setTimeout(() => resolve(null), 2500)
      })
      return (await Promise.race([missionIdsPromise, timeoutPromise])) as string[] | null
    } catch {
      return null
    }
  })()

  if (myToken !== lifetimeLoadToken) return // superseded by a newer load
  if (idsJson == null || idsJson.length === 0) {
    lifetimeState.value = LifetimeLoadState.FailedTooFast
    return
  }

  const shouldFilter = dataFilter.value != null && dataFilter.value.length > 0 && filterLoad
  if (shouldFilter) {
    lifetimeDataLoadedProgress.value = { percentageDone: '0%', loadedCount: 0, totalCount: idsJson.length }
    lifetimeState.value = LifetimeLoadState.FilteringIds
  }

  const missions: DatabaseMission[] = shouldFilter
    ? (await globalThis.viewMissionsOfEid(activeAccountId.value!)) ?? []
    : []

  const idFilter = async (arr: string[], predicate: (id: string) => Promise<boolean>): Promise<string[]> =>
    arr.reduce(
      async (memo, e) => ((await predicate(e)) ? [...(await memo), e] : await memo),
      Promise.resolve([] as string[]),
    )

  const ids: string[] = shouldFilter
    ? await idFilter(idsJson, async (id) => {
        lifetimeDataLoadedProgress.value.percentageDone = `${((idsJson.indexOf(id) + 1) / idsJson.length) * 100}%`
        lifetimeDataLoadedProgress.value.loadedCount++
        const mission = missions.find((m) => m.missionId === id)
        if (!mission) return false
        return await missionMatchesFilter(mission, dataFilter.value, orDataFilter.value)
      })
    : idsJson

  lifetimeDataExcludedCount.value = idsJson.length - ids.length

  const artifactsBucket = makeBucket()
  const stonesBucket = makeBucket()
  const stoneFragmentsBucket = makeBucket()
  const ingredientsBucket = makeBucket()

  const newData: LifetimeData = {
    artifacts: artifactsBucket.arr,
    stones: stonesBucket.arr,
    stoneFragments: stoneFragmentsBucket.arr,
    ingredients: ingredientsBucket.arr,
    missionCount: ids.length,
    mennoData: { configs: [], totalDropsCount: 0 },
  }
  if (myToken !== lifetimeLoadToken) return // superseded by a newer load
  lifetimeData.value = newData
  lifetimeState.value = LifetimeLoadState.LoadingMissionData
  lifetimeDataLoadedProgress.value = { percentageDone: '0%', loadedCount: 0, totalCount: ids.length }

  // Fetch mission metadata in one round-trip; drops are fetched per-mission to avoid
  // a single 12-15MB relay frame that can stall Chrome's WebSocket receive buffer and
  // permanently deadlock the CDP channel (blocking all subsequent ui.Eval calls).
  const missionCache = await globalThis.viewMissionsOfEid(activeAccountId.value!)
  const dropCache: Record<string, MissionDrop[]> | null = null

  let firstMatches: number[] | null = null
  let nonMatch = false
  let pos = 0
  for (const id of ids) {
    pos++
    const mission = await getSpecificMissionDataForLifetime(activeAccountId.value!, id, dropCache, missionCache)
    if (mission != null) {
      const mi = mission.missionInfo
      if (firstMatches == null) {
        firstMatches = [mi.ship, mi.durationType, mi.level, mi.targetInt]
      }
      if (!nonMatch) {
        if (firstMatches[0] !== mi.ship) firstMatches[0] = -1
        if (firstMatches[1] !== mi.durationType) firstMatches[1] = -1
        if (firstMatches[2] !== mi.level) firstMatches[2] = -1
        if (firstMatches[3] !== mi.targetInt) firstMatches[3] = -1
        nonMatch = firstMatches.includes(-1)
      }
      mission.artifacts.forEach((d) => mergeItems(artifactsBucket, d, mi))
      mission.stones.forEach((d) => mergeItems(stonesBucket, d, mi))
      mission.stoneFragments.forEach((d) => mergeItems(stoneFragmentsBucket, d, mi))
      mission.ingredients.forEach((d) => mergeItems(ingredientsBucket, d, mi))
    }
    lifetimeDataLoadedProgress.value.percentageDone = `${(pos / ids.length) * 100}%`
    lifetimeDataLoadedProgress.value.loadedCount = pos
    // Yield to the browser task queue every 50 iterations so the progress bar
    // actually repaints during the loop rather than only at the end.
    if (pos % 50 === 0) {
      await new Promise<void>((r) => setTimeout(r, 0))
      if (myToken !== lifetimeLoadToken) return // superseded - stop the expensive loop
    }
  }

  reSortLifetime()

  lifetimeAllowExpectedTotals.value = lifetimeHasExactlyOneConfiguration(firstMatches)
  if (lifetimeAllowExpectedTotals.value && firstMatches != null) {
    const shipArg = Number.parseInt(lifetimeOneShipInt.value ?? String(firstMatches[0]))
    const durationArg = Number.parseInt(lifetimeOneDurationInt.value ?? String(firstMatches[1]))
    const levelArg = Number.parseInt(lifetimeOneLevelInt.value ?? String(firstMatches[2]))
    const targetRaw = Number.parseInt(lifetimeOneTargetInt.value ?? String(firstMatches[3]))
    const targetArg = targetRaw === -1 ? 10000 : targetRaw
    const mennoConfigItems = await getMennoData(shipArg, durationArg, levelArg, targetArg)
    if (myToken !== lifetimeLoadToken) return // superseded by a newer load
    if (mennoConfigItems && lifetimeData.value) {
      lifetimeData.value.mennoData = {
        configs: mennoConfigItems as unknown as LedgerData['mennoData']['configs'],
        totalDropsCount: mennoConfigItems.reduce(
          (acc: number, cur: { totalDrops: number }) => acc + cur.totalDrops,
          0,
        ),
      }
    }
  }

  const end = performance.now()
  lifetimeSuccessTime.value =
    Math.floor((end - start) / 1000) + '.' + Math.floor((end - start) % 1000) + 's'
  lifetimeDataBeingFiltered.value = false
  filterHasChanged.value = false
  lifetimeState.value = LifetimeLoadState.Success
}

// Auto-load on account change (and on first mount via immediate), mirroring the
// Mission Data tab so the user never has to click "Load Lifetime Data". The
// manual button remains as a fallback if the auto-load times out.
// Reload on account CHANGE. The first/initial load is kicked off from onMounted
// (after shared configs are loaded), so this watch is intentionally not
// immediate - otherwise the first auto-load would run before configs are ready.
watch(activeAccountId, (id) => {
  clearFilter()
  lifetimeData.value = null
  lifetimeState.value = LifetimeLoadState.Idle
  if (id) viewLifetimeDataOfEid(false)
})

// Refresh when a fetch completes, so freshly fetched missions show up without a
// manual reload (again matching Mission Data).
watch(appState, (val) => {
  if (val === AppState.Success && activeAccountId.value) {
    viewLifetimeDataOfEid(false)
  }
})

async function onFilterSubmit(event: Event) {
  event.preventDefault()
  await viewLifetimeDataOfEid(true)
}

// Lifecycle

onMounted(async () => {
  await loadSharedConfigs()
  await loadMennoData()

  lifetimeSortMethod.value = (await globalThis.getLifetimeSortMethod()) as 'default' | 'iv' | 'count' | 'random'
  lifetimeShowDropsPerShip.value = await globalThis.getLifetimeShowDropsPerShip()
  lifetimeShowExpectedTotalsPref.value = await globalThis.getLifetimeShowExpectedTotals()
  settingsLoaded.value = true

  // Pre-cache filter value options
  getFilterValueOptions('ship')
  getFilterValueOptions('duration')
  getFilterValueOptions('level')
  getFilterValueOptions('target')
  getFilterValueOptions('drops')

  // Initial auto-load now that shared configs are ready. (The activeAccountId
  // watch above handles later account changes; if the account is still loading
  // from storage it will fire that watch once it resolves.)
  if (activeAccountId.value) viewLifetimeDataOfEid(false)
})
</script>
