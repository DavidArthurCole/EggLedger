<template>
  <div class="flex-1 flex flex-col w-full mx-auto px-4 space-y-3 overflow-hidden bg-darker">
    <!-- Target filter overlay -->
    <SearchOverSelector
      :item-list="lifetimeTargetSelectList"
      ledger-type="target"
      :is-lifetime="true"
      @close="closeTargetFilterMenu"
      @select="selectTargetFilter"
      @input="(val: string) => (lifetimeTargetSearchTerm = val)"
    />

    <!-- Drop filter overlay -->
    <SearchOverSelector
      :item-list="lifetimeDropSelectList"
      ledger-type="drop"
      :is-lifetime="true"
      @close="closeDropFilterMenu"
      @select="selectDropFilter"
      @input="(val: string) => (lifetimeDropSearchTerm = val)"
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
      <div class="relative flex-grow focus-within:z-10">
        <div
          v-if="selectedLifetimeAccount != null && accountById(selectedLifetimeAccount) != null"
          class="ledger-input-overlay"
        >
          <span class="whitespace-pre">{{ accountById(selectedLifetimeAccount)?.id }}</span>
          (<span :style="'color: #' + (accountById(selectedLifetimeAccount)?.accountColor || '')">
            {{ accountById(selectedLifetimeAccount)?.nickname }}
            {{ accountById(selectedLifetimeAccount)?.ebString }}
          </span>
          - {{ accountById(selectedLifetimeAccount)?.missionCount }} missions)
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
            {{ account.id }}
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
            accountById(selectedLifetimeAccount) == null ||
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
          :disabled="lifetimeDataBeingFiltered || !lifetimeFilterHasChanged"
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
    <div
      v-if="!doesDataExist"
      class="text-center mt-3rem rounded-md border border-red-700 py-2"
    >
      <span class="text-red-700">
        No mission data has been loaded yet.<br />Please <b>Fetch</b> from the
      </span>
      <button type="button" class="btn-link" @click="activeTab = 'Ledger'">
        <span class="text-blue-500 ledger-underline font-bold">Ledger tab</span>
      </button>
      <span class="text-red-700"> and then come back here.</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useAppState } from '../composables/useAppState'
import { useMennoData } from '../composables/useMennoData'
import type {
  DatabaseMission,
  MissionDrop,
  PossibleTarget,
  PossibleArtifact,
  PossibleMission,
} from '../types/bridge'
import FullFilter from '../components/FullFilter.vue'
import SearchOverSelector from '../components/SearchOverSelector.vue'
import DropDisplayContainer, { type LedgerData } from '../components/DropDisplayContainer.vue'

// ───────────────────────────────────────────────────────────────────────────────
// Shared state
// ───────────────────────────────────────────────────────────────────────────────

const { existingData, activeTab } = useAppState()
const { mennoDataLoaded, getMennoData, load: loadMennoData } = useMennoData()

// ───────────────────────────────────────────────────────────────────────────────
// Types local to this view
// ───────────────────────────────────────────────────────────────────────────────

enum LifetimeLoadState {
  Idle = 'Idle',
  FetchingIds = 'FetchingIds',
  FilteringIds = 'FilteringIds',
  LoadingMissionData = 'LoadingMissionData',
  Success = 'Success',
  Failed = 'Failed',
  FailedTooFast = 'FailedTooFast',
}

interface FilterCondition {
  topLevel: string
  op: string
  val: string
}

interface FilterOption {
  text: string
  value: string
  styleClass?: string
  imagePath?: string
  rarity?: number
  rarityGif?: string
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

interface HandleFilterChangeEventTarget extends HTMLElement {
  oldValue?: string | null
  value?: string
  type?: string
}

// ───────────────────────────────────────────────────────────────────────────────
// Account selector state
// ───────────────────────────────────────────────────────────────────────────────

const selectedLifetimeAccount = ref<string | null>(null)
const accountDropdownOpen = ref(false)

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

function openAccountDropdown() {
  accountDropdownOpen.value = true
}
function closeAccountDropdown(id?: string) {
  if (id != null && id !== '') selectedLifetimeAccount.value = id
  accountDropdownOpen.value = false
}

// ───────────────────────────────────────────────────────────────────────────────
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
  ld.artifacts = sortFn(ld.artifacts as unknown as DropLike[]) as unknown as LifetimeDrop[]
  ld.stones = sortFn(ld.stones as unknown as DropLike[]) as unknown as LifetimeDrop[]
  ld.stoneFragments = sortFn(ld.stoneFragments as unknown as DropLike[]) as unknown as LifetimeDrop[]
  ld.ingredients = sortFn(ld.ingredients as unknown as DropLike[]) as unknown as LifetimeDrop[]
}

// ───────────────────────────────────────────────────────────────────────────────
// Configs used by the filter value options
// ───────────────────────────────────────────────────────────────────────────────

const possibleTargets = ref<PossibleTarget[]>([])
const maxQuality = ref<number>(0)
const artifactConfigs = ref<PossibleArtifact[]>([])
const durationConfigs = ref<PossibleMission[]>([])

function artifactDisplayText(artifact: PossibleArtifact): string {
  const displayName = artifact.displayName
  const level = artifact.level
  let displayText = displayName
  if (String(level) !== '%') {
    const isStoneNotFragment =
      displayName.toLowerCase().includes('stone') &&
      !displayName.toLowerCase().includes('fragment')
    displayText += ' (T' + (Number(level) + (isStoneNotFragment ? 2 : 1)) + ')'
  }
  return displayText
}

function dropPath(drop: PossibleArtifact): string {
  const addendum = drop.protoName.includes('_STONE') ? 1 : 0
  const fixedName = drop.protoName
    .replaceAll('_FRAGMENT', '')
    .replaceAll('ORNATE_GUSSET', 'GUSSET')
    .replaceAll('VIAL_MARTIAN_DUST', 'VIAL_OF_MARTIAN_DUST')
  return 'artifacts/' + fixedName + '/' + fixedName + '_' + (drop.level + 1 + addendum) + '.png'
}

function dropRarityPath(drop: PossibleArtifact): string {
  switch (drop.rarity) {
    case 1:
      return 'images/rare.gif'
    case 2:
      return 'images/epic.gif'
    case 3:
      return 'images/legendary.gif'
    default:
      return ''
  }
}

function getFilterValueOptions(topLevel: string | null): FilterOption[] {
  switch (topLevel) {
    case 'ship':
      return Array.from({ length: 11 }, (_, index) => ({
        text: [
          'Chicken One', 'Chicken Nine', 'Chicken Heavy',
          'BCR', 'Quintillion Chicken', 'Cornish-Hen Corvette',
          'Galeggtica', 'Defihent', 'Voyegger', 'Henerprise', 'Atreggies Henliner',
        ][index],
        value: String(index),
      }))
    case 'duration':
      return Array.from({ length: 4 }, (_, index) => ({
        text: ['Short', 'Standard', 'Extended', 'Tutorial'][index],
        value: String(index),
        styleClass: 'text-duration-' + index,
      }))
    case 'level':
      return Array.from({ length: 9 }, (_, index) => ({
        text: index + '\u2605',
        value: String(index),
      }))
    case 'target':
      return possibleTargets.value.map((target) => ({
        text: target.displayName,
        value: String(target.id),
        imagePath: target.imageString,
      }))
    case 'drops': {
      const filtered: FilterOption[] = artifactConfigs.value
        .filter((a) => a.baseQuality <= maxQuality.value)
        .map((artifact) => ({
          text: artifactDisplayText(artifact),
          value: artifact.name + '_' + artifact.level + '_' + artifact.rarity + '_' + artifact.baseQuality,
          rarity: artifact.rarity,
          imagePath: dropPath(artifact),
          rarityGif: dropRarityPath(artifact),
        }))
      filtered.unshift({ text: 'Any Legendary', value: '%_%_3_%', rarity: 3, styleClass: 'text-legendary', imagePath: 'icon_help.webp' })
      filtered.unshift({ text: 'Any Epic', value: '%_%_2_%', rarity: 2, styleClass: 'text-epic', imagePath: 'icon_help.webp' })
      filtered.unshift({ text: 'Any Rare', value: '%_%_1_%', rarity: 1, styleClass: 'text-rare', imagePath: 'icon_help.webp' })
      return filtered
    }
    case 'buggedcap':
    case 'dubcap':
      return [{ text: 'True', value: 'true' }, { text: 'False', value: 'false' }]
    default:
      return []
  }
}

// ───────────────────────────────────────────────────────────────────────────────
// Filter state (lifetime-local copy)
// ───────────────────────────────────────────────────────────────────────────────

const lifetimeFilterConditionsCount = ref(1)
const lifetimeFilterTopLevel = ref<(string | null)[]>([])
const lifetimeFilterOperators = ref<(string | null)[]>([])
const lifetimeFilterValues = ref<(string | null)[]>([])

const lifetimeOrFilterConditionsCount = ref<(number | null)[]>([])
const lifetimeOrFilterTopLevel = ref<(string | null)[][]>([[]])
const lifetimeOrFilterOperators = ref<(string | null)[][]>([[]])
const lifetimeOrFilterValues = ref<(string | null)[][]>([[]])

const dLifetimeFilterValues = ref<(string | null)[]>([])
const dLifetimeOrFilterValues = ref<(string | null)[][]>([[]])

const lifetimeDataFilter = ref<FilterCondition[]>([])
const lifetimeOrDataFilter = ref<FilterCondition[][]>([[]])
const hideLifetimeFilter = ref(false)
const lifetimeFilterHasChanged = ref(false)

watch(lifetimeFilterValues, () => {
  dLifetimeFilterValues.value = lifetimeFilterValues.value.map(
    (_f, index) => (dLifetimeFilterValues.value[index] === undefined ? '' : dLifetimeFilterValues.value[index]),
  )
}, { deep: true })

watch(lifetimeOrFilterValues, () => {
  dLifetimeOrFilterValues.value = lifetimeOrFilterValues.value.map((orFilter, index) =>
    dLifetimeOrFilterValues.value[index] === undefined
      ? []
      : dLifetimeOrFilterValues.value[index].slice(0, orFilter?.length ?? 0),
  )
}, { deep: true })

watch([lifetimeDataFilter, lifetimeOrDataFilter], () => {
  lifetimeFilterHasChanged.value = true
})

// "Has exactly one <X>" watchers used to decide if Menno data applies
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
    const matches = lifetimeFilterTopLevel.value.filter((f) => f === kind)
    if (matches.length !== 1) continue
    const idx = lifetimeFilterTopLevel.value.indexOf(kind)
    const op = lifetimeFilterOperators.value[idx]
    const val = lifetimeFilterValues.value[idx]
    if (op == null || val == null || op !== '=') continue
    flagRef.value = true
    valRef.value = val
  }
}
watch(
  [lifetimeFilterTopLevel, lifetimeFilterOperators, lifetimeFilterValues],
  refreshExactlyOneFlags,
  { deep: true },
)

function clearLifetimeFilter() {
  lifetimeFilterTopLevel.value = []
  lifetimeFilterOperators.value = []
  lifetimeFilterValues.value = []
  lifetimeFilterConditionsCount.value = 1
  lifetimeOrFilterConditionsCount.value = []
  lifetimeOrFilterTopLevel.value = [[]]
  lifetimeOrFilterOperators.value = [[]]
  lifetimeOrFilterValues.value = [[]]
  dLifetimeFilterValues.value = []
  dLifetimeOrFilterValues.value = [[]]
  lifetimeDataFilter.value = []
  lifetimeOrDataFilter.value = [[]]
  lifetimeFilterHasChanged.value = false
}

// ───────────────────────────────────────────────────────────────────────────────
// Filter handlers
// ───────────────────────────────────────────────────────────────────────────────

function changeFilterValue(
  index: number | string,
  orIndex: number | string | null,
  level: string,
  value: string,
) {
  const intIndex = typeof index === 'number' ? index : Number.parseInt(index)
  let intOrIndex: number | null = null
  if (orIndex != null) {
    intOrIndex = typeof orIndex === 'number' ? orIndex : Number.parseInt(orIndex)
  }
  switch (level) {
    case 'top':
      if (intOrIndex == null) lifetimeFilterTopLevel.value[intIndex] = value
      else lifetimeOrFilterTopLevel.value[intIndex][intOrIndex] = value
      break
    case 'operator':
      if (intOrIndex == null) lifetimeFilterOperators.value[intIndex] = value
      else lifetimeOrFilterOperators.value[intIndex][intOrIndex] = value
      break
    case 'value':
      if (intOrIndex == null) lifetimeFilterValues.value[intIndex] = value
      else lifetimeOrFilterValues.value[intIndex][intOrIndex] = value
      break
  }
}

// eslint-disable-next-line sonarjs/cognitive-complexity
function updateValueStyling(passedEl: HTMLElement, isOr: boolean) {
  const idParts = passedEl.id.split('-')
  // Lifetime filter IDs are suffixed with "-lifetime"
  // Example: filter-value-0-lifetime  or  filter-value-0-2-lifetime (or)
  // We strip the trailing "-lifetime" and reparse.
  const trimmed = idParts.filter((p) => p !== 'lifetime')
  const index = Number.parseInt(trimmed[2])
  const orIndex = isOr ? Number.parseInt(trimmed[trimmed.length - 1]) : null
  if (isOr && orIndex == null) return
  const el = document.getElementById('filter-value-' + index + (isOr ? '-' + orIndex : '') + '-lifetime')
  if (!el) return
  const classMatchBorder = el.className.match(/ border-(.*?)(\s|$)/)
  const classMatchText = el.className.replace('text-sm', ' ').match(/ text-(.*?)(\s|$)/)
  const existingBorder = classMatchBorder ? 'border-' + classMatchBorder[1].trim() : ''
  const existingText = classMatchText ? 'text-' + classMatchText[1].trim() : ''
  const isDrops = isOr
    ? lifetimeOrFilterTopLevel.value[index][orIndex!] === 'drops'
    : lifetimeFilterTopLevel.value[index] === 'drops'
  const dropsValue = isOr
    ? lifetimeOrFilterValues.value[index][orIndex!]
    : lifetimeFilterValues.value[index]
  const isDuration = isOr
    ? lifetimeOrFilterTopLevel.value[index][orIndex!] === 'duration'
    : lifetimeFilterTopLevel.value[index] === 'duration'
  let newBorder = ''
  let newText = ''

  if (isDrops && dropsValue != null && dropsValue !== '') {
    switch (dropsValue.split('_')[2]) {
      case '1': newBorder = 'border-rare'; newText = 'text-rarity-1'; break
      case '2': newBorder = 'border-epic'; newText = 'text-rarity-2'; break
      case '3': newBorder = 'border-legendary'; newText = 'text-rarity-3'; break
      default: newBorder = 'border-gray-300'; newText = 'text-gray-400'; break
    }
  } else if (isDuration && (el as HTMLInputElement).value !== '') {
    switch ((el as HTMLInputElement).value) {
      case '0': newBorder = 'border-short'; newText = 'text-duration-0'; break
      case '1': newBorder = 'border-standard'; newText = 'text-duration-1'; break
      case '2': newBorder = 'border-extended'; newText = 'text-duration-2'; break
      case '3': newBorder = 'border-tutorial'; newText = 'text-duration-3'; break
      default: newBorder = 'border-gray-300'; newText = 'text-gray-400'; break
    }
  } else {
    newBorder = 'border-gray-300'
    newText = 'text-gray-400'
  }
  if (existingBorder) el.classList.replace(existingBorder, newBorder)
  else el.classList.add(newBorder)
  if (existingText) el.classList.replace(existingText, newText)
  else el.classList.add(newText)
}

// eslint-disable-next-line sonarjs/cognitive-complexity
function updateFilterNoReCount() {
  lifetimeFilterHasChanged.value = true
  const newDataFilter: FilterCondition[] = []
  if (lifetimeFilterTopLevel.value.length !== 0 && lifetimeFilterTopLevel.value != null) {
    for (let i = 0; i < lifetimeFilterTopLevel.value.length; i++) {
      const topLevel = lifetimeFilterTopLevel.value[i]
      const op = lifetimeFilterOperators.value[i]
      const val = lifetimeFilterValues.value[i]
      if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) break
      newDataFilter.push({ topLevel, op, val })
    }
  }
  lifetimeDataFilter.value = newDataFilter

  const newOrDataFilter: FilterCondition[][] = []
  for (let i = 0; i < lifetimeOrFilterConditionsCount.value.length; i++) {
    const arr: FilterCondition[] = []
    if (
      lifetimeOrFilterTopLevel.value[i] == null ||
      lifetimeOrFilterOperators.value[i] == null ||
      lifetimeOrFilterValues.value[i] == null
    ) continue
    for (let j = 0; j < lifetimeOrFilterTopLevel.value[i].length; j++) {
      const topLevel = lifetimeOrFilterTopLevel.value[i][j]
      const op = lifetimeOrFilterOperators.value[i][j]
      const val = lifetimeOrFilterValues.value[i][j]
      if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) break
      arr.push({ topLevel, op, val })
    }
    newOrDataFilter.push(arr)
  }
  lifetimeOrDataFilter.value = newOrDataFilter
}

function updateFilterConditionsCount() {
  lifetimeFilterHasChanged.value = true
  const newDataFilter: FilterCondition[] = []
  if (lifetimeFilterTopLevel.value.length === 0 || lifetimeFilterTopLevel.value == null) return
  let maxIndex = 0
  let set = false

  for (let i = 0; i < lifetimeFilterTopLevel.value.length; i++) {
    const topLevel = lifetimeFilterTopLevel.value[i]
    const op = lifetimeFilterOperators.value[i]
    const val = lifetimeFilterValues.value[i]

    if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) {
      maxIndex = i
      break
    }
    newDataFilter.push({ topLevel, op, val })
    if (i + 1 === lifetimeFilterConditionsCount.value) {
      lifetimeFilterConditionsCount.value += 1
      set = true
    }
  }
  if (maxIndex === 0) {
    maxIndex = lifetimeFilterTopLevel.value.filter((f) => f != null && f !== '').length
  }

  if (!set) lifetimeFilterConditionsCount.value = maxIndex + 1
  lifetimeDataFilter.value = newDataFilter
}

function handleFilterChange(eventOrId: Event | string) {
  const targetEl =
    typeof eventOrId === 'string'
      ? (document.getElementById(eventOrId) as HandleFilterChangeEventTarget | null)
      : ((eventOrId.target as HandleFilterChangeEventTarget) ?? null)
  if (!targetEl) return
  const targetId = targetEl.id
  if (targetEl.oldValue == null || targetEl.type === 'date') {
    updateFilterConditionsCount()
    targetEl.oldValue = targetEl.value ?? ''
  } else updateFilterNoReCount()
  updateValueStyling(targetEl, false)
  if (targetId.includes('filter-top')) {
    const idParts = targetId.split('-').filter((p) => p !== 'lifetime')
    const index = Number.parseInt(idParts[idParts.length - 1])
    const options = getFilterValueOptions(targetEl.value ?? '')
    const opts = new Set(options.map((o) => String(o.value)))
    if (!opts.has(String(lifetimeFilterOperators.value[index]))) {
      lifetimeFilterOperators.value[index] = null
    }
    if (!opts.has(String(lifetimeFilterValues.value[index]))) {
      lifetimeFilterValues.value[index] = null
      dLifetimeFilterValues.value[index] = null
    }
  }
}

function handleOrFilterChange(eventOrId: Event | string) {
  const targetEl =
    typeof eventOrId === 'string'
      ? (document.getElementById(eventOrId) as HandleFilterChangeEventTarget | null)
      : ((eventOrId.target as HandleFilterChangeEventTarget) ?? null)
  if (!targetEl) return
  const targetId = targetEl.id
  const idParts = targetId.split('-').filter((p) => p !== 'lifetime')
  const index = Number.parseInt(idParts[2])
  const orIndex = Number.parseInt(idParts[idParts.length - 1])
  updateFilterNoReCount()
  updateValueStyling(targetEl, true)
  if (targetId.includes('filter-top')) {
    const options = getFilterValueOptions(targetEl.value ?? '')
    const opts = new Set(options.map((o) => String(o.value)))
    if (!opts.has(String(lifetimeOrFilterOperators.value[index][orIndex]))) {
      lifetimeOrFilterOperators.value[index][orIndex] = null
    }
    if (!opts.has(String(lifetimeOrFilterValues.value[index][orIndex]))) {
      lifetimeOrFilterValues.value[index][orIndex] = null
      dLifetimeOrFilterValues.value[index][orIndex] = null
    }
  }
}

function addOr(index: number) {
  if (lifetimeOrFilterConditionsCount.value == null) lifetimeOrFilterConditionsCount.value = []
  if (lifetimeOrFilterConditionsCount.value[index] == null) lifetimeOrFilterConditionsCount.value[index] = 1
  else
    lifetimeOrFilterConditionsCount.value[index] =
      (lifetimeOrFilterConditionsCount.value[index] as number) + 1

  if (lifetimeOrFilterTopLevel.value[index] == null) lifetimeOrFilterTopLevel.value[index] = []
  if (lifetimeOrFilterOperators.value[index] == null) lifetimeOrFilterOperators.value[index] = []
  if (lifetimeOrFilterValues.value[index] == null) lifetimeOrFilterValues.value[index] = []

  lifetimeOrFilterTopLevel.value[index].push(null)
  lifetimeOrFilterOperators.value[index].push(null)
  lifetimeOrFilterValues.value[index].push(null)
}

function removeAndShift(index: number) {
  for (let i = index; i < lifetimeFilterTopLevel.value.length; i++) {
    lifetimeFilterTopLevel.value[i] = lifetimeFilterTopLevel.value[i + 1]
    lifetimeFilterOperators.value[i] = lifetimeFilterOperators.value[i + 1]
    lifetimeFilterValues.value[i] = lifetimeFilterValues.value[i + 1]
    dLifetimeFilterValues.value[i] = dLifetimeFilterValues.value[i + 1]

    lifetimeOrFilterConditionsCount.value[i] = lifetimeOrFilterConditionsCount.value[i + 1]
    lifetimeOrFilterTopLevel.value[i] = lifetimeOrFilterTopLevel.value[i + 1]
    lifetimeOrFilterOperators.value[i] = lifetimeOrFilterOperators.value[i + 1]
    lifetimeOrFilterValues.value[i] = lifetimeOrFilterValues.value[i + 1]
    dLifetimeOrFilterValues.value[i] = dLifetimeOrFilterValues.value[i + 1]
  }
  updateFilterConditionsCount()
  updateFilterNoReCount()
  lifetimeFilterHasChanged.value = true

  setTimeout(() => {
    document.querySelectorAll("[id^='filter-top-']").forEach((el) => {
      if ((el as HTMLElement).id.endsWith('-lifetime')) updateValueStyling(el as HTMLElement, false)
    })
  }, 10)
}

function removeOrAndShift(index: number, orIndex: number) {
  for (let i = orIndex; i < lifetimeOrFilterTopLevel.value[index].length; i++) {
    lifetimeOrFilterTopLevel.value[index][i] = lifetimeOrFilterTopLevel.value[index][i + 1]
    lifetimeOrFilterOperators.value[index][i] = lifetimeOrFilterOperators.value[index][i + 1]
    lifetimeOrFilterValues.value[index][i] = lifetimeOrFilterValues.value[index][i + 1]
    dLifetimeOrFilterValues.value[index][i] = dLifetimeOrFilterValues.value[index][i + 1]
  }
  const cur = lifetimeOrFilterConditionsCount.value[index]
  if (cur != null) {
    const next = cur - 1
    lifetimeOrFilterConditionsCount.value[index] = next === 0 ? null : next
  }
  updateFilterNoReCount()
  lifetimeFilterHasChanged.value = true
}

function generateFilterConditionsArr() {
  return new Array(lifetimeFilterConditionsCount.value)
}

function filterModVals() {
  return {
    top: lifetimeFilterTopLevel.value,
    operator: lifetimeFilterOperators.value,
    value: lifetimeFilterValues.value,
    dValue: dLifetimeFilterValues.value,
    orTop: lifetimeOrFilterTopLevel.value,
    orOperator: lifetimeOrFilterOperators.value,
    orValue: lifetimeOrFilterValues.value,
    orDValue: dLifetimeOrFilterValues.value,
    orCount: lifetimeOrFilterConditionsCount.value,
  }
}

// ───────────────────────────────────────────────────────────────────────────────
// Mission-against-filter logic (for filtered lifetime loads)
// ───────────────────────────────────────────────────────────────────────────────

function ledgerDate(timestamp: number): Date {
  return new Date(timestamp * 1000)
}
function ledgerDateObj(date: string): Date {
  const parts = date.split('-')
  return new Date(Number.parseInt(parts[0]), Number.parseInt(parts[1]) - 1, Number.parseInt(parts[2]))
}

function commonFilterLogic(
  value: unknown,
  filterValue: unknown,
  operator: string,
  currentState: boolean,
): boolean {
  switch (operator) {
    case 'd=':
      if ((value as Date).toDateString() !== (filterValue as Date).toDateString()) return false
      break
    case '=':
      if (value != filterValue) return false
      break
    case '!=':
      if (value == filterValue) return false
      break
    case '>':
      if ((value as number) <= (filterValue as number)) return false
      break
    case '<':
      if ((value as number) >= (filterValue as number)) return false
      break
    case 'true':
      if (!value) return false
      break
    case 'false':
      if (value) return false
      break
    default:
      return currentState
  }
  return currentState
}

// eslint-disable-next-line sonarjs/cognitive-complexity
async function testMissionAgainstFilter(
  mission: DatabaseMission,
  filter: FilterCondition,
): Promise<boolean> {
  let filterPassed = true
  if (filter.topLevel != null && filter.op != null && filter.val != null) {
    switch (filter.topLevel) {
      case 'buggedcap':
        filterPassed = commonFilterLogic(mission.isBuggedCap, null, filter.val, filterPassed)
        break
      case 'dubcap':
        filterPassed = commonFilterLogic(mission.isDubCap, null, filter.val, filterPassed)
        break
      case 'ship':
        filterPassed = commonFilterLogic(mission.ship, filter.val, filter.op, filterPassed)
        break
      case 'duration':
        filterPassed = commonFilterLogic(mission.durationType, filter.val, filter.op, filterPassed)
        break
      case 'level':
        filterPassed = commonFilterLogic(mission.level, filter.val, filter.op, filterPassed)
        break
      case 'target':
        filterPassed = commonFilterLogic(mission.targetInt, filter.val, filter.op, filterPassed)
        break
      case 'launchDT':
        filterPassed = commonFilterLogic(
          ledgerDate(mission.launchDT),
          ledgerDateObj(filter.val),
          filter.op,
          filterPassed,
        )
        break
      case 'returnDT':
        filterPassed = commonFilterLogic(
          ledgerDate(mission.returnDT),
          ledgerDateObj(filter.val),
          filter.op,
          filterPassed,
        )
        break
      case 'drops': {
        const shipIdx = mission.ship
        const durIdx = mission.durationType
        const shipConfig = durationConfigs.value[shipIdx]
        if (!shipConfig) {
          filterPassed = false
          break
        }
        const durConfig = shipConfig.durations[durIdx]
        if (!durConfig) {
          filterPassed = false
          break
        }
        const maxQual = durConfig.maxQuality + durConfig.levelQualityBump * mission.level
        const filterQuality = filter.val.split('_')[3]
        const qualityBypass = filterQuality === '%'
        const filterRarity = filter.val.split('_')[2]
        const rarityBypass = filterRarity === '%'
        const filterLevel = filter.val.split('_')[1]
        const levelBypass = filterLevel === '%'
        const filterName = filter.val.split('_')[0]
        const nameBypass = filterName === '%'
        const allDrops = await globalThis.getShipDrops(selectedLifetimeAccount.value ?? '', mission.missionId)
        if (allDrops == null) filterPassed = false
        else {
          switch (filter.op) {
            case 'c': {
              let foundDrop = false
              if (
                (!qualityBypass && maxQual < Number.parseFloat(filterQuality)) ||
                (!qualityBypass && durConfig.minQuality > Number.parseFloat(filterQuality))
              ) {
                filterPassed = false
              }
              for (const drop of allDrops) {
                if (!nameBypass && Number.parseInt(filterName) !== drop.id) continue
                if (!levelBypass && Number.parseInt(filterLevel) !== drop.level) continue
                if (!rarityBypass && Number.parseInt(filterRarity) !== drop.rarity) continue
                foundDrop = true
              }
              if (!foundDrop) filterPassed = false
              break
            }
            case 'dnc': {
              for (const drop of allDrops) {
                if (!nameBypass && Number.parseInt(filterName) !== drop.id) continue
                if (!levelBypass && Number.parseInt(filterLevel) !== drop.level) continue
                if (!rarityBypass && Number.parseInt(filterRarity) !== drop.rarity) continue
                filterPassed = false
              }
              break
            }
          }
        }
        break
      }
    }
    return filterPassed
  }
  return false
}

// eslint-disable-next-line sonarjs/cognitive-complexity
async function missionMatchesFilter(
  mission: DatabaseMission,
  filters: FilterCondition[],
  orFilters: FilterCondition[][],
): Promise<boolean> {
  let allFiltersPassed = true
  let index = 0
  for (const filter of filters) {
    let filterPassed = true
    if (filter.topLevel != null && filter.op != null && (filter.val != null || filter.topLevel === 'target')) {
      filterPassed = await testMissionAgainstFilter(mission, filter)
      if (!filterPassed) {
        if (orFilters[index] != null) {
          for (const orFilter of orFilters[index]) {
            if (orFilter.topLevel != null && orFilter.op != null && (orFilter.val != null || orFilter.topLevel === 'target')) {
              filterPassed = await testMissionAgainstFilter(mission, orFilter)
              if (filterPassed) break
            }
          }
        }
        if (!filterPassed) allFiltersPassed = false
      }
    }
    index++
  }
  return allFiltersPassed
}

// ───────────────────────────────────────────────────────────────────────────────
// Drop/target filter overlays (lifetime)
// ───────────────────────────────────────────────────────────────────────────────

const lifetimeDropSelectList = ref<FilterOption[]>([])
const lifetimeDropFilterSelectedIndex = ref<number | null>(null)
const lifetimeDropFilterSelectedOrIndex = ref<number | null>(null)
const lifetimeDropFilterMenuOpen = ref(false)
const lifetimeDropSearchTerm = ref('')

const lifetimeTargetSelectList = ref<FilterOption[]>([])
const lifetimeTargetFilterSelectedIndex = ref<number | null>(null)
const lifetimeTargetFilterSelectedOrIndex = ref<number | null>(null)
const lifetimeTargetFilterMenuOpen = ref(false)
const lifetimeTargetSearchTerm = ref('')

watch(lifetimeDropSearchTerm, () => {
  lifetimeDropSelectList.value = getFilterValueOptions('drops').filter((d) =>
    d.text.toLowerCase().includes(lifetimeDropSearchTerm.value.toLowerCase()),
  )
})
watch(lifetimeTargetSearchTerm, () => {
  lifetimeTargetSelectList.value = getFilterValueOptions('target').filter((t) =>
    t.text.toLowerCase().includes(lifetimeTargetSearchTerm.value.toLowerCase()),
  )
})

function openDropFilterMenu(index: number, orIndex: number | null) {
  lifetimeDropSelectList.value = getFilterValueOptions('drops')
  lifetimeDropFilterSelectedIndex.value = index
  lifetimeDropFilterSelectedOrIndex.value = orIndex ?? null
  lifetimeDropFilterMenuOpen.value = true
  const el = document.querySelector('.overlay-drop-lifetime') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
  const input = document.getElementById('drop-search-lifetime') as HTMLInputElement | null
  input?.focus()
}
function closeDropFilterMenu() {
  lifetimeDropSearchTerm.value = ''
  lifetimeDropFilterSelectedIndex.value = null
  lifetimeDropFilterSelectedOrIndex.value = null
  lifetimeDropFilterMenuOpen.value = false
  const el = document.querySelector('.overlay-drop-lifetime') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
}
function selectDropFilter(drop: FilterOption) {
  const newValue = drop.value.toString().split('.')[0]
  if (lifetimeDropFilterSelectedIndex.value != null) {
    if (lifetimeDropFilterSelectedOrIndex.value == null) {
      lifetimeFilterValues.value[lifetimeDropFilterSelectedIndex.value] = newValue
      dLifetimeFilterValues.value[lifetimeDropFilterSelectedIndex.value] = drop.text
      handleFilterChange(`filter-value-${lifetimeDropFilterSelectedIndex.value}-lifetime`)
      const el = document.getElementById(`filter-value-${lifetimeDropFilterSelectedIndex.value}-lifetime`) as HTMLInputElement | null
      if (el) el.size = drop.text.length
    } else {
      lifetimeOrFilterValues.value[lifetimeDropFilterSelectedIndex.value][lifetimeDropFilterSelectedOrIndex.value] = newValue
      dLifetimeOrFilterValues.value[lifetimeDropFilterSelectedIndex.value][lifetimeDropFilterSelectedOrIndex.value] = drop.text
      handleOrFilterChange(
        `filter-value-${lifetimeDropFilterSelectedIndex.value}-${lifetimeDropFilterSelectedOrIndex.value}-lifetime`,
      )
      const el = document.getElementById(
        `filter-value-${lifetimeDropFilterSelectedIndex.value}-${lifetimeDropFilterSelectedOrIndex.value}-lifetime`,
      ) as HTMLInputElement | null
      if (el) el.size = drop.text.length
    }
  }
  closeDropFilterMenu()
}

function openTargetFilterMenu(index: number, orIndex: number | null) {
  lifetimeTargetSelectList.value = getFilterValueOptions('target')
  lifetimeTargetFilterSelectedIndex.value = index
  lifetimeTargetFilterSelectedOrIndex.value = orIndex ?? null
  lifetimeTargetFilterMenuOpen.value = true
  const el = document.querySelector('.overlay-target-lifetime') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
  const input = document.getElementById('target-search-lifetime') as HTMLInputElement | null
  input?.focus()
}
function closeTargetFilterMenu() {
  lifetimeTargetSearchTerm.value = ''
  lifetimeTargetFilterSelectedIndex.value = null
  lifetimeTargetFilterSelectedOrIndex.value = null
  lifetimeTargetFilterMenuOpen.value = false
  const el = document.querySelector('.overlay-target-lifetime') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
}
function selectTargetFilter(target: FilterOption) {
  const newValue = target.value.toString().split('.')[0]
  if (lifetimeTargetFilterSelectedIndex.value != null) {
    if (lifetimeTargetFilterSelectedOrIndex.value == null) {
      lifetimeFilterValues.value[lifetimeTargetFilterSelectedIndex.value] = newValue
      dLifetimeFilterValues.value[lifetimeTargetFilterSelectedIndex.value] = target.text
      handleFilterChange(`filter-value-${lifetimeTargetFilterSelectedIndex.value}-lifetime`)
      const el = document.getElementById(
        `filter-value-${lifetimeTargetFilterSelectedIndex.value}-lifetime`,
      ) as HTMLInputElement | null
      if (el) el.size = target.text.length
    } else {
      lifetimeOrFilterValues.value[lifetimeTargetFilterSelectedIndex.value][lifetimeTargetFilterSelectedOrIndex.value] = newValue
      dLifetimeOrFilterValues.value[lifetimeTargetFilterSelectedIndex.value][lifetimeTargetFilterSelectedOrIndex.value] = target.text
      handleOrFilterChange(
        `filter-value-${lifetimeTargetFilterSelectedIndex.value}-${lifetimeTargetFilterSelectedOrIndex.value}-lifetime`,
      )
      const el = document.getElementById(
        `filter-value-${lifetimeTargetFilterSelectedIndex.value}-${lifetimeTargetFilterSelectedOrIndex.value}-lifetime`,
      ) as HTMLInputElement | null
      if (el) el.size = target.text.length
    }
  }
  closeTargetFilterMenu()
}

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

  const shouldFilter = lifetimeDataFilter.value != null && lifetimeDataFilter.value.length > 0 && filterLoad
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
        return await missionMatchesFilter(mission, lifetimeDataFilter.value, lifetimeOrDataFilter.value)
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
  lifetimeFilterHasChanged.value = false
  lifetimeState.value = LifetimeLoadState.Success
}

async function onViewSubmit(event: Event) {
  event.preventDefault()
  clearLifetimeFilter()
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
