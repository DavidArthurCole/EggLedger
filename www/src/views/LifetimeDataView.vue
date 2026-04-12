<template>
  <div class="flex-1 flex flex-col w-full mx-auto px-4 space-y-3 overflow-hidden bg-darker">
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

    <!-- Account selector -->
    <form
      v-if="doesDataExist"
      style="margin-top: 0rem !important"
      id="lifetimeAccountForm"
      name="lifetimeAccountForm"
      class="select-form"
      @submit="onViewSubmit"
    >
      <div ref="accountSelectRef" class="relative flex-grow focus-within:z-10">
        <div
          v-if="selectedLifetimeAccountData != null"
          class="ledger-input-overlay"
        >
          <span class="whitespace-pre">{{ maskEid(selectedLifetimeAccountData.id) }}</span>
          (<span :style="'color: #' + selectedLifetimeAccountData.accountColor">
            {{ selectedLifetimeAccountData.nickname }}
            {{ selectedLifetimeAccountData.ebString }}
          </span>
          - {{ selectedLifetimeAccountData.missionCount }} missions
          <template v-if="selectedLifetimeKnownAccount?.seString">
            <span class="text-gray-400">&nbsp;·</span>
            <img :src="'images/soul_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
            <span style="color:#a855f7">{{ selectedLifetimeKnownAccount.seString }} SE</span>
            <span class="text-gray-400"> ·</span>
            <img :src="'images/prophecy_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
            <span style="color:#eab308">{{ selectedLifetimeKnownAccount.peCount }} PE</span>
            <template v-if="selectedLifetimeKnownAccount.teCount">
              <span class="text-gray-400"> ·</span>
              <img :src="'images/truth_egg.png'" style="display:inline;height:1em;vertical-align:middle;margin:0 0.25em" alt="">
              <span style="color:#c831ff">{{ selectedLifetimeKnownAccount.teCount }} TE</span>
            </template>
          </template>)
        </div>
        <input
          id="lifetimeAccountInput"
          type="text"
          class="drop-select border-gray-300"
          placeholder="Select an account"
          :value="selectedLifetimeAccount ?? ''"
          @focus="openAccountDropdown"
          @input="(e) => (selectedLifetimeAccount = (e.target as HTMLInputElement).value)"
        />
        <ul
          v-if="accountDropdownOpen && objectedExistingData.length > 0"
          class="ledger-list focus:outline-none sm:text-sm"
          tabindex="-1"
        >
          <li
            v-for="account in objectedExistingData"
            :key="account.id"
            class="drop-opt"
            @click="closeAccountDropdown(account.id)"
          >
            {{ maskEid(account.id) }}
            (<span :style="'color: #' + account.accountColor">{{ account.nickname }} {{ account.ebString }}</span>
            - {{ account.missionCount }} missions)
          </li>
        </ul>
      </div>
      <button
        class="-ml-px relative w-20 text-center space-x-2 px-4 py-2 border border-gray-300 text-sm font-medium rounded-r-md text-gray-400 filter-button"
        type="submit"
        :disabled="
          !selectedLifetimeAccount ||
            selectedLifetimeAccount === '' ||
            selectedLifetimeAccountData == null ||
            lifetimeDataBeingLoaded
        "
      >
        View
      </button>
    </form>

    <!-- Lifetime progress -->
    <div v-if="doesDataExist" class="h-14 px-2 py-1 text-sm text-gray-400 bg-darkest rounded-md tabular-nums">
      <div v-if="lifetimeState !== LifetimeLoadState.Idle">
        <div>
          <img
            v-if="isProgressSpinning"
            :src="'images/loading.gif'"
            alt="Loading..."
            class="target-ico"
          />
          <span :class="statusColor">{{ statusText }}</span>
        </div>
        <div class="h-3 relative rounded-full overflow-hidden mt-1">
          <div class="w-full h-full bg-dark absolute"></div>
          <div
            v-if="lifetimeState === LifetimeLoadState.Failed || lifetimeState === LifetimeLoadState.FailedTooFast"
            class="h-full absolute rounded-full bg-data_loss_red w-full"
          ></div>
          <div
            v-else
            class="h-full absolute rounded-full bg-green-500"
            :style="{ width: lifetimeDataLoadedProgress.percentageDone }"
          ></div>
        </div>
      </div>
    </div>

    <!-- Filter panel -->
    <div
      v-if="doesDataExist"
      class="min-h-7 px-2 py-1 text-gray-400 bg-darkest rounded-md tabular-nums overflow-auto mt-0_75rem"
    >
      <span
        v-if="(lifetimeData != null && !lifetimeDataBeingLoaded) || lifetimeDataBeingFiltered"
        class="h-20 font-bold text-gray-400"
      >Mission Filter: </span>
      <button
        v-if="(lifetimeData != null && !lifetimeDataBeingLoaded) || lifetimeDataBeingFiltered"
        id="toggleLifetimeFilterButton"
        class="text-base toggle-link"
        type="button"
        @click="(e) => { e.preventDefault(); hideLifetimeFilter = !hideLifetimeFilter }"
      >
        {{ hideLifetimeFilter ? 'Show' : 'Hide' }}
      </button>
      <form
        v-if="(!hideLifetimeFilter && lifetimeData != null && !lifetimeDataBeingLoaded) || lifetimeDataBeingFiltered"
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
          id="lifetime-filter-apply-button"
          type="submit"
          class="mt-0_5rem mr-1rem -ml-px relative p-0.5 text-center space-x-2 px-3 py-1 border border-gray-300 text-sm font-medium rounded-md text-gray-400 bg-darkerer hover:bg-dark_tab_hover disabled:opacity-50 disabled:hover:darker_tab_hover disabled:hover:cursor-not-allowed focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
          :disabled="lifetimeDataBeingFiltered || !filterHasChanged"
        >
          Apply Filter
        </button>
      </form>
    </div>

    <!-- Data display -->
    <div
      v-if="doesDataExist"
      class="flex-1 px-2 py-1 overflow-auto shadow-sm block text-xs font-mono text-gray-300 bg-darkest rounded-md text-center"
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
        <div class="mb-0_5rem">
          <span>Sort method</span>
          <br />
          <span class="mr-5">
            <label for="lifetimeSortDefault" class="ext-opt-label">Default</label>
            <input id="lifetimeSortDefault" v-model="lifetimeSortMethod" type="radio" value="default" class="ext-opt-check" />
          </span>
          <span class="mr-5">
            <label for="lifetimeSortIV" class="ext-opt-label">Inventory Visualizer</label>
            <input id="lifetimeSortIV" v-model="lifetimeSortMethod" type="radio" value="iv" class="ext-opt-check" />
          </span>
          <span class="mr-5">
            <label for="lifetimeSortTotalCount" class="ext-opt-label">Total Count</label>
            <input id="lifetimeSortTotalCount" v-model="lifetimeSortMethod" type="radio" value="count" class="ext-opt-check" />
          </span>
          <span class="mr-5">
            <label for="lifetimeSortRandom" class="ext-opt-label">Random</label>
            <input id="lifetimeSortRandom" v-model="lifetimeSortMethod" type="radio" value="random" class="ext-opt-check" />
          </span>
        </div>
        <div>
          <div class="mb-0_5rem">
            <span>Options</span>
            <br />
            <span class="mr-5">
              <label for="lifetimeDataPerShip" class="ext-opt-label">Show 'Average Drops per Ship'</label>
              <input id="lifetimeDataPerShip" v-model="lifetimeShowDropsPerShip" type="checkbox" class="ext-opt-check" />
            </span>
            <span v-if="lifetimeAllowExpectedTotals">
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
import { useDropdownSelector } from '../composables/useDropdownSelector'
import { maskEid } from '../composables/useSettings'
import type {
  DatabaseMission,
  MissionDrop,
  PossibleTarget,
  PossibleArtifact,
  PossibleMission,
} from '../types/bridge'
import FullFilter from '../components/FullFilter.vue'
import NoDataFallback from '../components/NoDataFallback.vue'
import SearchOverSelector from '../components/SearchOverSelector.vue'
import DropDisplayContainer, { type LedgerData } from '../components/DropDisplayContainer.vue'

// Shared state

const { existingData, knownAccounts, activeTab } = useAppState()
const { mennoDataLoaded, getMennoData, load: loadMennoData } = useMennoData()

// Types local to this view

enum LifetimeLoadState {
  Idle = 'Idle',
  FetchingIds = 'FetchingIds',
  FilteringIds = 'FilteringIds',
  LoadingMissionData = 'LoadingMissionData',
  Success = 'Success',
  Failed = 'Failed',
  FailedTooFast = 'FailedTooFast',
}

interface LifetimeDataLoadedProgress {
  percentageDone: string
  loadedCount: number
  totalCount: number
}

interface DropLike {
  id: number
  name: string
  level: number
  rarity: number
  quality: number
  ivOrder: number
  specType: string
  count?: number
}

interface LifetimeDrop extends MissionDrop {
  count: number
  missionInfos?: DatabaseMission[]
  digShipInfo?: boolean
}

// LifetimeData matches LedgerData (for DropDisplayContainer)
interface LifetimeData extends LedgerData {
  missionCount: number
  artifacts: LifetimeDrop[]
  stones: LifetimeDrop[]
  stoneFragments: LifetimeDrop[]
  ingredients: LifetimeDrop[]
}

// ───────────────────────────────────────────────────────────────────────────────
// Account selector state
// ───────────────────────────────────────────────────────────────────────────────

const selectedLifetimeAccount = ref<string | null>(null)

const {
  containerRef: accountSelectRef,
  isOpen: accountDropdownOpen,
  open: openAccountDropdown,
  close: closeAccountDropdown,
} = useDropdownSelector((id) => { selectedLifetimeAccount.value = id })

const doesDataExist = computed(() => existingData.value.length > 0)

const objectedExistingData = computed(() => {
  return existingData.value
    .map((account) => ({
      id: account.id,
      nickname: account.nickname,
      missionCount: account.missionCount,
      ebString: account.ebString && account.ebString !== '' ? account.ebString : '???',
      accountColor: account.accountColor,
    }))
    .sort((a, b) => b.missionCount - a.missionCount)
})

function accountById(id: string | null) {
  if (id == null) return null
  return objectedExistingData.value.find((acc) => acc.id === id) ?? null
}

const selectedLifetimeAccountData = computed(
  () => accountById(selectedLifetimeAccount.value),
)
const selectedLifetimeKnownAccount = computed(
  () => knownAccounts.value.find((acc) => acc.id === selectedLifetimeAccount.value) ?? null,
)

// Lifetime load state
// ───────────────────────────────────────────────────────────────────────────────

const lifetimeState = ref<LifetimeLoadState>(LifetimeLoadState.Idle)
const lifetimeSuccessTime = ref<string | null>(null)
const lifetimeData = ref<LifetimeData | null>(null)
const lifetimeDataBeingFiltered = ref(false)
const lifetimeDataExcludedCount = ref(0)
const lifetimeDataLoadedProgress = ref<LifetimeDataLoadedProgress>({
  percentageDone: '0%',
  loadedCount: 0,
  totalCount: 0,
})

const lifetimeDataBeingLoaded = computed(
  () =>
    lifetimeState.value === LifetimeLoadState.FetchingIds ||
    lifetimeState.value === LifetimeLoadState.LoadingMissionData,
)

const isProgressSpinning = computed(
  () =>
    lifetimeState.value === LifetimeLoadState.FetchingIds ||
    lifetimeState.value === LifetimeLoadState.FilteringIds ||
    lifetimeState.value === LifetimeLoadState.LoadingMissionData,
)

const statusColor = computed(() => {
  switch (lifetimeState.value) {
    case LifetimeLoadState.Success:
      return 'text-green-500'
    case LifetimeLoadState.Failed:
    case LifetimeLoadState.FailedTooFast:
      return 'text-red-700'
    default:
      return 'text-gray-300'
  }
})

const statusText = computed(() => {
  switch (lifetimeState.value) {
    case LifetimeLoadState.FetchingIds:
      return 'Fetching mission IDs...'
    case LifetimeLoadState.FilteringIds:
      return `Filtering mission IDs... ${lifetimeDataLoadedProgress.value.loadedCount}/${lifetimeDataLoadedProgress.value.totalCount}`
    case LifetimeLoadState.LoadingMissionData:
      return `Loading missions... ${lifetimeDataLoadedProgress.value.loadedCount}/${lifetimeDataLoadedProgress.value.totalCount}`
    case LifetimeLoadState.Success:
      return 'Succeeded' + (lifetimeSuccessTime.value == null ? '' : ` in ${lifetimeSuccessTime.value}`)
    case LifetimeLoadState.Failed:
      return 'Failed'
    case LifetimeLoadState.FailedTooFast:
      return 'Failed: Data loaded too quickly after start. Try again.'
    default:
      return ''
  }
})

// ───────────────────────────────────────────────────────────────────────────────
// Sorting options
// ───────────────────────────────────────────────────────────────────────────────

const lifetimeSortMethod = ref<'default' | 'iv' | 'count' | 'random'>('default')
const lifetimeShowDropsPerShip = ref(false)
const lifetimeAllowExpectedTotals = ref(false)
const lifetimeShowExpectedTotalsPref = ref(false)
const lifetimeShowExpectedTotals = computed(
  () => lifetimeAllowExpectedTotals.value && lifetimeShowExpectedTotalsPref.value,
)

watch(lifetimeSortMethod, () => reSortLifetime())
watch(mennoDataLoaded, () => {
  if (!mennoDataLoaded.value) lifetimeShowExpectedTotalsPref.value = false
})

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
  ld.artifacts = sortFn([...ld.artifacts] as unknown as DropLike[]) as unknown as LifetimeDrop[]
  ld.stones = sortFn([...ld.stones] as unknown as DropLike[]) as unknown as LifetimeDrop[]
  ld.stoneFragments = sortFn([...ld.stoneFragments] as unknown as DropLike[]) as unknown as LifetimeDrop[]
  ld.ingredients = sortFn([...ld.ingredients] as unknown as DropLike[]) as unknown as LifetimeDrop[]
}

// Artifact/mission configs (loaded in onMounted, passed to useFilters)
const possibleTargets = ref<PossibleTarget[]>([])
const maxQuality = ref<number>(0)
const artifactConfigs = ref<PossibleArtifact[]>([])
const durationConfigs = ref<PossibleMission[]>([])

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
  accountId: selectedLifetimeAccount,
  durationConfigs, possibleTargets, maxQuality, artifactConfigs,
})

// View-specific filter state
const hideLifetimeFilter = ref(false)

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

// ───────────────────────────────────────────────────────────────────────────────
// Lifetime fetch pipeline
// ───────────────────────────────────────────────────────────────────────────────

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

async function getSpecificMissionDataForLifetime(eid: string, missionId: string) {
  const missionInfo = await globalThis.getMissionInfo(eid, missionId)
  if (missionInfo.ship == null || missionInfo.ship < 0 || !missionInfo.missionId) return null
  const allDrops = await globalThis.getShipDrops(eid, missionId)
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

// eslint-disable-next-line sonarjs/cognitive-complexity
async function viewLifetimeDataOfEid(filterLoad: boolean) {
  if (!selectedLifetimeAccount.value) return
  lifetimeDataBeingFiltered.value = filterLoad

  const start = performance.now()
  lifetimeState.value = LifetimeLoadState.FetchingIds

  // Fetch mission IDs with a 2.5 second timeout
  const idsJson: string[] | null = await (async () => {
    try {
      const missionIdsPromise = globalThis.getMissionIds(selectedLifetimeAccount.value!)
      const timeoutPromise = new Promise<null>((resolve) => {
        setTimeout(() => resolve(null), 2500)
      })
      return (await Promise.race([missionIdsPromise, timeoutPromise])) as string[] | null
    } catch {
      return null
    }
  })()

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
    ? (await globalThis.viewMissionsOfEid(selectedLifetimeAccount.value!)) ?? []
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
  lifetimeData.value = newData
  lifetimeState.value = LifetimeLoadState.LoadingMissionData
  lifetimeDataLoadedProgress.value = { percentageDone: '0%', loadedCount: 0, totalCount: ids.length }

  let firstMatches: number[] | null = null
  let nonMatch = false
  for (const id of ids) {
    const mission = await getSpecificMissionDataForLifetime(selectedLifetimeAccount.value!, id)
    if (mission == null) continue
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

    lifetimeDataLoadedProgress.value.percentageDone = `${((ids.indexOf(id) + 1) / ids.length) * 100}%`
    lifetimeDataLoadedProgress.value.loadedCount++
  }

  reSortLifetime()

  lifetimeAllowExpectedTotals.value = lifetimeHasExactlyOneConfiguration(firstMatches)
  if (lifetimeAllowExpectedTotals.value && firstMatches != null) {
    const shipArg = Number.parseInt(lifetimeOneShipInt.value ?? String(firstMatches[0]))
    const durationArg = Number.parseInt(lifetimeOneDurationInt.value ?? String(firstMatches[1]))
    const levelArg = Number.parseInt(lifetimeOneLevelInt.value ?? String(firstMatches[2]))
    const targetRaw = Number.parseInt(lifetimeOneTargetInt.value ?? String(firstMatches[3]))
    const targetArg = targetRaw === -1 ? 1000 : targetRaw
    const mennoConfigItems = await getMennoData(shipArg, durationArg, levelArg, targetArg)
    if (mennoConfigItems) {
      newData.mennoData = {
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

async function onViewSubmit(event: Event) {
  event.preventDefault()
  clearFilter()
  await viewLifetimeDataOfEid(false)
}

async function onFilterSubmit(event: Event) {
  event.preventDefault()
  await viewLifetimeDataOfEid(true)
}

// ───────────────────────────────────────────────────────────────────────────────
// Lifecycle
// ───────────────────────────────────────────────────────────────────────────────

onMounted(async () => {
  artifactConfigs.value = await globalThis.getAfxConfigs()
  maxQuality.value = await globalThis.getMaxQuality()
  durationConfigs.value = await globalThis.getDurationConfigs()
  possibleTargets.value = await globalThis.getPossibleTargets()

  await loadMennoData()

  // Pre-cache filter value options
  getFilterValueOptions('ship')
  getFilterValueOptions('duration')
  getFilterValueOptions('level')
  getFilterValueOptions('target')
  getFilterValueOptions('drops')
})
</script>
