<template>
  <div class="flex-1 flex flex-col w-full mx-auto px-4 overflow-hidden bg-darker">
    <SearchOverSelector
      v-if="dropFilterMenuOpen"
      :item-list="dropSelectList"
      ledger-type="drop"
      :is-lifetime="false"
      @close="closeDropFilterMenu"
      @select="selectDropFilter"
      @input="(val: string) => dropSearchTerm = val"
    />

    <SearchOverSelector
      v-if="targetFilterMenuOpen"
      :item-list="targetSelectList"
      ledger-type="target"
      :is-lifetime="false"
      @close="closeTargetFilterMenu"
      @select="selectTargetFilter"
      @input="(val: string) => targetSearchTerm = val"
    />

    <!-- Single mission overlay -->
    <MissionOverlay
      mode="single"
      :open="boolMissionBeingViewed"
      :mission-data="viewMissionData"
      :show-expected-drops="showExpectedDropsPerShip"
      @close="closeMissionOverlay"
      @view="(val: string) => viewSpecificMission(val)"
    />

    <!-- Multi-mission overlay -->
    <MissionOverlay
      mode="multi"
      :open="multiMissionOverlayOpen"
      :multi-mission-data="multiViewMissionData"
      :is-loading="rowViewBeingLoaded"
      :drop-cache-preloading="dropCachePreloading"
      :total-to-load="multiViewTotalToLoad"
      :loaded-count="missionsBeingViewed.length"
      :show-expected-drops="showExpectedDropsPerShip"
      :sort-method="viewMissionSortMethod"
      @close="closeMultiMissionOverlay"
    />


    <!-- Mission load progress -->
    <SegmentedProgressBar
      v-if="doesDataExist"
      :active="eidMissionsBeingLoaded"
      :segments="missionLoadSegments"
      :status-text="missionLoadStatusText"
      :is-spinning="eidMissionsBeingLoaded"
    />

    <!-- Filter panel -->
    <div
      v-if="doesDataExist && hasAnyFilterableData"
      class="filter-panel"
    >
      <span
        class="mr-0_5rem h-20 font-bold text-gray-400"
      >Filter</span>
      <button
        id="toggleFilterButton"
        class="text-base toggle-link"
        type="button"
        @click="toggleFilter"
      >
        {{ hideFilter ? 'Show' : 'Hide' }}
      </button>
      <form
        v-if="!hideFilter"
        id="filterFormVM"
        name="filterFormVM"
        class="filter-form text-xs"
        @submit.prevent="applyFilter"
      >
        <div
          v-if="!filterWarningRead && !hideFilterWarning"
          class="text-red-700 border border-red-700 rounded-md mb-1 mt-1 py-2 px-4"
        >
          <span class="font-bold ledger-underline mb-0_25rem">Warning:</span><br />
          The filter feature is experimental, and may not work as expected (especially with complicated combinations).
          You may experience unexpected results, including lag spikes, potential app crashes, and UI distortion.
          Please use it carefully, and report any issues you encounter.
          <br /><br />
          <button
            type="button"
            class="btn-link dismiss-btn"
            @click="dismissFilterWarning"
          >I understand</button>
        </div>

        <FullFilter
          :filter-array="generateFilterConditionsArr()"
          :mod-vals="filterModVals()"
          :is-lifetime="false"
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
          id="filter-apply-button"
          type="submit"
          class="apply-filter-button"
          :disabled="dataBeingFiltered || !filterHasChanged"
        >
          Apply Filter
        </button>

        <span v-if="dataBeingFiltered" class="text-green-700 mt-0_5rem">
          <i>
            <img :src="'images/loading.gif'" alt="Loading..." class="target-ico" />
            Applying filter, please wait...
          </i>
        </span>
        <span v-if="!dataBeingFiltered && filterApplyTime !== ''" class="text-green-700 mt-0_5rem">
          <i>
            Filtered in {{ filterApplyTime }}
            <span class="text-gray-500">
              (
              <span class="text-green-700">
                {{ filteredMissions?.length ?? 0 }} shown<span class="text-gray-500">,</span>
                {{ (allLoadedMissions?.length ?? 0) - (filteredMissions?.length ?? 0) }} filtered out
              </span>)
            </span>
          </i>
        </span>
      </form>
    </div>

    <!-- Options panel -->
    <div
      v-if="doesDataExist && allLoadedMissions != null && !eidMissionsBeingLoaded"
      class="min-h-7 max-h-50 px-2 py-2 text-sm text-gray-400 bg-darkest rounded-md tabular-nums overflow-auto mt-0_75rem"
    >
      <OptionsPanel
        :has-any-filterable-data="hasAnyFilterableData"
        :menno-data-loaded="mennoDataLoaded"
        v-model:view-by-date="viewByDate"
        v-model:view-mission-times="viewMissionTimes"
        v-model:recolor-d-c="recolorDC"
        v-model:recolor-b-c="recolorBC"
        v-model:show-expected-drops-per-ship="showExpectedDropsPerShip"
        v-model:multi-view-mode="multiViewMode"
        v-model:view-mission-sort-method="viewMissionSortMethod"
      />
    </div>

    <!-- Mission type tabs: only shown when loaded missions include both Home and Virtue -->
    <div v-if="hasBothMissionTypes" class="flex gap-1 mt-0_75rem text-sm">
      <button
        v-for="tab in [{ label: 'All', value: null }, { label: 'Home', value: 0 }, { label: 'Virtue', value: 1 }]"
        :key="String(tab.value)"
        type="button"
        :class="[
          'px-3 py-1 rounded-md border font-medium transition-colors',
          missionTypeTab === tab.value
            ? 'bg-darkest text-gray-200 border-gray-500'
            : 'bg-darker text-gray-400 border-gray-700 hover:bg-dark_tab_hover',
        ]"
        @click="missionTypeTab = tab.value"
      >
        {{ tab.label }}
        <span v-if="tab.value === null" class="text-gray-500 ml-1">({{ filteredMissions?.length ?? 0 }})</span>
        <span v-else class="text-gray-500 ml-1">({{ filteredMissions?.filter(m => m.missionType === tab.value).length ?? 0 }})</span>
      </button>
    </div>

    <!-- Mission tree -->
    <div
      v-if="doesDataExist"
      class="flex-1 min-h-0 px-2 overflow-auto shadow-sm bg-darkest rounded-md mt-0_75rem"
    >
      <MissionResultsTable
        v-if="hasAnyFilterableData"
        :grouped-missions="groupedMissions"
        :grouped-arrays="groupedArrays"
        :all-visible="allVisible"
        :view-by-date="viewByDate"
        :view-mission-times="viewMissionTimes"
        :recolor-d-c="recolorDC"
        :recolor-b-c="recolorBC"
        :multi-view-mode="multiViewMode"
        :multi-view-free-select-ids="multiViewFreeSelectIds"
        :filtered-missions="tabFilteredMissions"
        :mission-being-viewed="missionBeingViewed"
        @view-mission="viewSpecificMission($event)"
        @toggle-elements="toggleElements"
        @multi-view-selection="handleMultiViewSelection"
        @trigger-row-view="triggerRowView"
        @deselect-all="multiViewFreeSelectIds = []"
        @select-all="multiViewFreeSelectIds = (tabFilteredMissions ?? []).map(m => m.missionId)"
      />
    </div>

    <!-- No data fallback -->
    <NoDataFallback v-if="!doesDataExist" @navigate="activeTab = $event" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useAppState } from '../composables/useAppState'
import { useMennoData } from '../composables/useMennoData'
import { useFetch } from '../composables/useFetch'
import { useFilters } from '../composables/useFilters'
import { useActiveAccount } from '../composables/useActiveAccount'
import { collapseOlderSections } from '../composables/useSettings'
import { useSharedConfigs } from '../composables/useSharedConfigs'
import {
  type DropLike,
  sortGroupAlreadyCombed,
  sortedGroupedSpecType,
  inventoryVisualizerSort,
} from '../composables/useMissionSorting'
import { useMissionListGrouping } from '../composables/useMissionListGrouping'
import type {
  DatabaseMission,
  MissionDrop,
} from '../types/bridge'
import FullFilter from '../components/FullFilter.vue'
import SearchOverSelector from '../components/SearchOverSelector.vue'
import NoDataFallback from '../components/NoDataFallback.vue'
import SegmentedProgressBar, { type ProgressSegment } from '../components/SegmentedProgressBar.vue'
import OptionsPanel from '../components/OptionsPanel.vue'
import MissionResultsTable from '../components/MissionResultsTable.vue'
import MissionOverlay from '../components/MissionOverlay.vue'
import type { ViewMissionData, InnerDrop, MennoConfigItem } from '../types/missionView'

// Shared state

const { existingData, activeTab } = useAppState()
const { mennoDataLoaded, getMennoData, load: loadMennoData } = useMennoData()
const { isFetching } = useFetch()
const { activeAccountId } = useActiveAccount()
const { artifactConfigs, maxQuality, durationConfigs, possibleTargets, loadSharedConfigs } = useSharedConfigs()

// Account load state

const eidMissionsBeingLoaded = ref(false)
const loadedEid = ref<string | null>(null)

function missionLoadStatus(): ProgressSegment['status'] {
  if (eidMissionsBeingLoaded.value) return 'active'
  if (loadedEid.value !== null) return 'done'
  return 'pending'
}

const missionLoadSegments = computed((): ProgressSegment[] => [
  {
    label: 'Loading',
    status: missionLoadStatus(),
    color: 'blue',
    pulsing: eidMissionsBeingLoaded.value,
  },
])

const missionLoadStatusText = computed(() =>
  eidMissionsBeingLoaded.value ? 'Loading mission data...' : 'Done',
)

const doesDataExist = computed(() => existingData.value.length > 0)

// Mission list + filter result state

const allLoadedMissions = ref<DatabaseMission[] | null>(null)
const filteredMissions = ref<DatabaseMission[] | null>(null)
const missionTypeTab = ref<number | null>(null) // null=All, 0=Home, 1=Virtue

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

// Viewing option state

const viewByDate = ref(false)
const viewMissionTimes = ref(true)
const recolorDC = ref(false)
const recolorBC = ref(false)
const showExpectedDropsPerShip = ref(true)
const hideFilter = ref(false)
const multiViewMode = ref<'off' | 'row' | 'free'>('off')
const viewMissionSortMethod = ref<'default' | 'iv'>('default')

// Suppress setter calls while loading initial values from Go in onMounted.
// Without this, every ref that differs from its default triggers a redundant
// write-back to Go storage immediately after being read from it.
let settingsLoaded = false

watch(mennoDataLoaded, () => {
  if (!mennoDataLoaded.value) showExpectedDropsPerShip.value = false
})

watch(viewByDate, (val) => { if (settingsLoaded) globalThis.setMissionViewByDate(val) })
watch(viewMissionTimes, (val) => { if (settingsLoaded) globalThis.setMissionViewTimes(val) })
watch(recolorDC, (val) => { if (settingsLoaded) globalThis.setMissionRecolorDC(val) })
watch(recolorBC, (val) => { if (settingsLoaded) globalThis.setMissionRecolorBC(val) })
watch(showExpectedDropsPerShip, (val) => { if (settingsLoaded) globalThis.setMissionShowExpectedDrops(val) })
watch(multiViewMode, (val) => { if (settingsLoaded) globalThis.setMissionMultiViewMode(val) })
watch(viewMissionSortMethod, (val) => { if (settingsLoaded) globalThis.setMissionSortMethod(val) })

function toggleFilter(event: Event) {
  event.preventDefault()
  hideFilter.value = !hideFilter.value
}

// Filter composable

const {
  dataFilter,
  orDataFilter,
  filterHasChanged,
  getFilterValueOptions,
  changeFilterValue,
  handleFilterChange,
  handleOrFilterChange,
  addOr,
  removeAndShift,
  removeOrAndShift,
  generateFilterConditionsArr,
  filterModVals,
  clearFilter,
  ledgerDate,
  missionMatchesFilter,
  dropSelectList,
  dropFilterMenuOpen,
  dropSearchTerm,
  targetSelectList,
  targetFilterMenuOpen,
  targetSearchTerm,
  openDropFilterMenu,
  closeDropFilterMenu,
  selectDropFilter,
  openTargetFilterMenu,
  closeTargetFilterMenu,
  selectTargetFilter,
} = useFilters({
  accountId: loadedEid,
  durationConfigs,
  possibleTargets,
  maxQuality,
  artifactConfigs,
})

const { groupedArrays, allVisible, groupedMissions, toggleElements } =
  useMissionListGrouping(tabFilteredMissions, ledgerDate, collapseOlderSections, viewByDate)

const hasAnyFilterableData = computed(() => {
  if (groupedMissions.value && Object.keys(groupedMissions.value).length > 0) return true
  if (
    filteredMissions.value != null &&
    allLoadedMissions.value != null &&
    filteredMissions.value.length !== allLoadedMissions.value.length
  ) return true
  return false
})

// Filter warning state (unique to this view)

const filterApplyTime = ref('')
const dataBeingFiltered = ref(false)
const filterWarningRead = ref(false)
const hideFilterWarning = ref(false)

async function dismissFilterWarning() {
  await globalThis.setFilterWarningRead(true)
  filterWarningRead.value = true
  hideFilterWarning.value = true
}

// Apply filter (on-demand after data loaded, unique to this view)

async function applyFilter() {
  if (!allLoadedMissions.value) return
  const startTime = performance.now()
  multiViewFreeSelectIds.value = []
  dataBeingFiltered.value = true
  filterHasChanged.value = false
  const newFilteredMissions: DatabaseMission[] = []
  for (const loadedMission of allLoadedMissions.value) {
    if (await missionMatchesFilter(loadedMission, dataFilter.value, orDataFilter.value)) {
      newFilteredMissions.push(loadedMission)
    }
  }
  const endTime = performance.now()
  filterApplyTime.value =
    Math.floor((endTime - startTime) / 1000) + '.' + Math.floor((endTime - startTime) % 1000) + 's'
  filteredMissions.value = newFilteredMissions
  dataBeingFiltered.value = false
}

// Mission view overlay state (single and multi)

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

// Fetch mission + view logic

async function loadMissions(id: string) {
  clearFilter()
  filterApplyTime.value = ''
  multiViewFreeSelectIds.value = []
  eidMissionsBeingLoaded.value = true
  allLoadedMissions.value = await globalThis.viewMissionsOfEid(id)
  filteredMissions.value = allLoadedMissions.value
  loadedEid.value = id
  eidMissionsBeingLoaded.value = false
}

watch(activeAccountId, (id) => {
  if (id) void loadMissions(id)
}, { immediate: true })

watch(isFetching, (val, prev) => {
  if (prev === true && !val && activeAccountId.value) {
    void loadMissions(activeAccountId.value)
  }
})

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
    const mennoTargetInt = mi.targetInt === -1 ? 1000 : (mi.targetInt ?? 1000)
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

function openUrl(url: string) {
  globalThis.openURL(url)
}

function formatShortDate(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

function properCaseTarget(target: string): string {
  return target
    .toLowerCase()
    .replaceAll('_', ' ')
    .split(' ')
    .map((w, i) => (i > 0 && (w === 'of' || w === 'the') ? w : w.charAt(0).toUpperCase() + w.slice(1)))
    .join(' ')
}

// Lifecycle

onMounted(async () => {
  filterWarningRead.value = (await globalThis.filterWarningRead()) ?? false

  viewByDate.value = await globalThis.getMissionViewByDate()
  viewMissionTimes.value = await globalThis.getMissionViewTimes()
  recolorDC.value = await globalThis.getMissionRecolorDC()
  recolorBC.value = await globalThis.getMissionRecolorBC()
  showExpectedDropsPerShip.value = await globalThis.getMissionShowExpectedDrops()
  multiViewMode.value = (await globalThis.getMissionMultiViewMode()) as 'off' | 'row' | 'free'
  viewMissionSortMethod.value = (await globalThis.getMissionSortMethod()) as 'default' | 'iv'

  await loadSharedConfigs()
  await loadMennoData()

  settingsLoaded = true

  // Pre-cache a few filter value options
  getFilterValueOptions('ship')
  getFilterValueOptions('duration')
  getFilterValueOptions('level')
  getFilterValueOptions('target')
  getFilterValueOptions('drops')

  // Click-outside handling for overlays
  document.querySelectorAll('.top-click-detect').forEach((topElement) => {
    const innerEl = topElement.querySelector('.inner-click-detect')
    if (!innerEl) return
    topElement.addEventListener('click', (event) => {
      if (innerEl.classList.contains('hidden')) return
      const divRect = innerEl.getBoundingClientRect()
      const me = event as MouseEvent
      if (
        me.clientX < divRect.left ||
        me.clientX > divRect.right ||
        me.clientY < divRect.top ||
        me.clientY > divRect.bottom
      ) {
        const trigger = innerEl.querySelector('.detect-trigger') as HTMLElement | null
        trigger?.click()
      }
    })
  })
})
</script>
