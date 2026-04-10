<template>
  <div class="flex-1 flex flex-col w-full mx-auto px-4 overflow-hidden bg-darker">
    <SearchOverSelector
      :item-list="dropSelectList"
      ledger-type="drop"
      :is-lifetime="false"
      @close="closeDropFilterMenu"
      @select="selectDropFilter"
      @input="(val: string) => dropSearchTerm = val"
    />

    <SearchOverSelector
      :item-list="targetSelectList"
      ledger-type="target"
      :is-lifetime="false"
      @close="closeTargetFilterMenu"
      @select="selectTargetFilter"
      @input="(val: string) => targetSearchTerm = val"
    />

    <!-- Single mission overlay -->
    <div class="top-click-detect mission-view-overlay overlay-mission">
      <div class="inner-click-detect max-w-70vw max-h-90vh overflow-auto bg-dark rounded-lg relative p-1rem">
        <button class="detect-trigger hover:text-gray-500 close-button" @click="closeMissionOverlay">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
        <ShipDisplay
          v-if="viewMissionData && viewMissionData.missionInfo != null"
          :view-mission-data="viewMissionData"
          :is-multi="false"
          :show-expected-drops="showExpectedDropsPerShip"
          @view="(val: string) => viewSpecificMission(val)"
        />
      </div>
    </div>

    <!-- Multi-mission overlay -->
    <div class="top-click-detect mission-view-overlay overlay-multi-mission">
      <div class="inner-click-detect max-w-90vw max-h-90vh bg-dark rounded-lg relative p-1rem">
        <button class="detect-trigger hover:text-gray-500 close-button" @click="closeMultiMissionOverlay">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
        <div v-if="rowViewBeingLoaded" class="text-center">
          <div class="mt-0_5rem text-center text-xl text-gray-400">Multi-Mission View Loading</div>
          <hr class="mt-1rem mb-1rem w-full" />
          <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
        </div>
        <div v-else class="max-w-90vw max-h-80vh overflow-auto">
          <div class="flex justify-between mb-4">
            <div
              v-for="(missionData, missionDataIndex) in multiViewMissionData"
              :key="missionDataIndex"
              :class="'w-1/' + multiViewMissionData.length"
            >
              <ShipDisplay
                v-if="missionData && missionData.missionInfo != null"
                :is-first="missionDataIndex === 0"
                :is-last="missionDataIndex === multiViewMissionData.length - 1"
                :view-mission-data="missionData"
                :is-multi="true"
                :show-expected-drops="showExpectedDropsPerShip"
                :ship-count="multiViewMissionData.length"
              />
            </div>
          </div>
          <div class="mt-2 text-xs text-gray-300 text-center">
            Hover mouse over an item to show details.<br />
            Click to open the relevant
            <a
              target="_blank"
              href="https://wasmegg-carpet.netlify.app/artifact-explorer/"
              class="ledger-underline"
              @click.prevent="openUrl('https://wasmegg-carpet.netlify.app/artifact-explorer/')"
            >
              artifact explorer
            </a> page.
          </div>
        </div>
      </div>
    </div>

    <!-- Account selector -->
    <form
      v-if="doesDataExist"
      id="viewAccountForm"
      name="viewAccountForm"
      class="select-form"
      @submit="onViewSubmit"
    >
      <div ref="viewMissionAccountSelectRef" class="relative flex-grow focus-within:z-10">
        <div
          v-if="selectedMissionAccount != null && accountById(selectedMissionAccount) != null"
          class="ledger-input-overlay"
        >
          <span class="whitespace-pre">{{ accountById(selectedMissionAccount)?.id }}</span>
          (<span :style="'color: #' + (accountById(selectedMissionAccount)?.accountColor || '')">
            {{ accountById(selectedMissionAccount)?.nickname }}
            {{ accountById(selectedMissionAccount)?.ebString }}
          </span>
          - {{ accountById(selectedMissionAccount)?.missionCount }} missions)
        </div>
        <input
          id="viewMissionAccountInput"
          type="text"
          class="drop-select border-gray-300"
          placeholder="Select an account"
          :value="selectedMissionAccount ?? ''"
          @focus="openAccountDropdown"
          @input="(e) => (selectedMissionAccount = (e.target as HTMLInputElement).value)"
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
          !selectedMissionAccount ||
            selectedMissionAccount === '' ||
            accountById(selectedMissionAccount) == null ||
            eidMissionsBeingLoaded
        "
      >
        View
      </button>
    </form>

    <!-- Filter panel -->
    <div
      v-if="doesDataExist"
      class="min-h-7 px-2 py-1 text-gray-400 bg-darkest rounded-md tabular-nums overflow-auto mt-0_75rem"
    >
      <span
        v-if="hasAnyFilterableData"
        class="mr-0_5rem h-20 font-bold text-gray-400"
      >Filter</span>
      <button
        v-if="hasAnyFilterableData"
        id="toggleFilterButton"
        class="text-base toggle-link"
        type="button"
        @click="toggleFilter"
      >
        {{ hideFilter ? 'Show' : 'Hide' }}
      </button>
      <form
        v-if="!hideFilter && hasAnyFilterableData"
        id="filterFormVM"
        name="filterFormVM"
        class="filter-form text-xs"
        @submit.prevent="applyFilter"
      >
        <div
          v-if="!filterWarningRead && !hideFilterWarning"
          class="text-red-700 border border-red-700 rounded-md mb-1 mt-1 py-2 px-4 max-w-50vw"
        >
          <span class="font-bold ledger-underline mb-0_25rem">Warning:</span><br />
          The filter feature is experimental, and may not work as expected (especially with complicated combinations).
          You may experience unexpected results, including lag spikes, potential app crashes, and UI distortion.
          Please use it carefully, and report any issues you encounter.
          <br /><br />
          <button
            type="button"
            class="p-0_75rem btn-link text-blue-500 border border-blue-500 rounded-md"
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
          class="mt-0_5rem mr-1rem -ml-px relative p-0.5 text-center space-x-2 px-3 py-1 border border-gray-300 text-sm font-medium rounded-md text-gray-400 bg-darkerer hover:bg-dark_tab_hover disabled:opacity-50 disabled:hover:darker_tab_hover disabled:hover:cursor-not-allowed focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
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
      v-if="doesDataExist"
      class="min-h-7 max-h-50 px-2 py-2 text-sm text-gray-400 bg-darkest rounded-md tabular-nums overflow-auto mt-0_75rem"
    >
      <div v-if="hasAnyFilterableData">
        <div>
          <span class="mr-0_5rem font-bold text-base text-gray-400">Options</span>
          <button
            id="toggleOptionsButtonVM"
            class="text-base toggle-link"
            type="button"
            @click="toggleOptions"
          >
            {{ hideOptions ? 'Show' : 'Hide' }}
          </button>
        </div>
        <div v-if="!hideOptions">
          <span class="opt-span">
            <label for="viewByDateCB" class="ext-opt-label">Separate missions by day</label>
            <input id="viewByDateCB" type="checkbox" v-model="viewByDate" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="viewMissionTimesCB" class="ext-opt-label">Show launch times</label>
            <input id="viewMissionTimesCB" type="checkbox" v-model="viewMissionTimes" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="recolorDCCB" class="ext-opt-label">Re-color dubcaps</label>
            <input id="recolorDCCB" type="checkbox" v-model="recolorDC" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="recolorBCCB" class="ext-opt-label">Re-color bugged-caps</label>
            <input id="recolorBCCB" type="checkbox" v-model="recolorBC" class="ext-opt-check" />
          </span>
          <span class="opt-span">
            <label for="showExpectedDropsPerShip" class="ext-opt-label">Show "Expected Drops Per Ship"</label>
            <input
              id="showExpectedDropsPerShip"
              type="checkbox"
              :disabled="!mennoDataLoaded"
              v-model="showExpectedDropsPerShip"
              class="ext-opt-check"
            />
          </span>
          <div>
            <span class="font-bold text-base text-gray-400">Multi-View Method</span><br />
            <span class="opt-span">
              <label for="multiViewOffCB" class="ext-opt-label">Off</label>
              <input id="multiViewOffCB" type="radio" v-model="multiViewMode" value="off" class="ext-opt-check" />
            </span>
            <span class="opt-span">
              <label for="multiViewRowCB" class="ext-opt-label">Row/Date Select</label>
              <input id="multiViewRowCB" type="radio" v-model="multiViewMode" value="row" class="ext-opt-check" />
            </span>
            <span class="opt-span">
              <label for="multiViewFreeCB" class="ext-opt-label">Free Select</label>
              <input id="multiViewFreeCB" type="radio" v-model="multiViewMode" value="free" class="ext-opt-check" />
            </span>
          </div>
          <div>
            <span class="font-bold text-base text-gray-400">Drops Sort Method</span><br />
            <span class="opt-span">
              <label for="viewMissionSortDefault" class="ext-opt-label">Default</label>
              <input
                id="viewMissionSortDefault"
                type="radio"
                v-model="viewMissionSortMethod"
                value="default"
                class="ext-opt-check"
              />
            </span>
            <span class="opt-span">
              <label for="viewMissionSortIV" class="ext-opt-label">Inventory Visualizer</label>
              <input
                id="viewMissionSortIV"
                type="radio"
                v-model="viewMissionSortMethod"
                value="iv"
                class="ext-opt-check"
              />
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- Mission tree -->
    <div
      v-if="doesDataExist"
      class="flex-1 px-2 overflow-auto shadow-sm bg-darkest rounded-md mt-0_75rem"
    >
      <div ref="resultsDiv" v-if="hasAnyFilterableData">
        <div class="filter-form text-lg" v-if="filteredMissions && filteredMissions.length === 0">
          <span class="mt-0_5rem ml-1rem">No missions to display.</span>
        </div>
        <div
          v-else
          id="missionListDiv"
          class="mt-0_5rem px-2 py-1 overflow-y-auto shadow-sm block text-xs font-mono text-gray-500 bg-darkest rounded-md"
        >
          <div class="bg-darkest w-full">
            <span class="text-xl">
              Missions
              <button
                v-if="filteredMissions && filteredMissions.length !== 0"
                id="toggleResultsButton"
                class="text-xl toggle-link"
                type="button"
                @click="toggleElements($event)"
              >
                {{ allVisible ? 'Collapse All' : 'Expand All' }}
              </button>
              <button
                v-if="multiViewMode === 'free' && multiViewFreeSelectIds.length > 0"
                class="text-xl view-multi-link"
                type="button"
                @click="triggerRowView()"
              >
                View Selected Missions
                <span class="text-gray-500">(</span>{{ multiViewFreeSelectIds.length }}<span class="text-gray-500">)</span>
              </button>
            </span>
            <hr class="mb-0_5rem w-full" />
          </div>
          <template v-for="(yearVF, yearIndex) in groupedMissions" :key="yearIndex">
            <span class="text-lg font-bold mr-0_5rem ledger-underline">{{ groupedArrays.year[yearIndex].year }}</span>
            <button
              class="tb-c text-lg toggle-link"
              type="button"
              @click="toggleElements($event, groupedArrays.year[yearIndex])"
            >
              {{ groupedArrays.year[yearIndex].enabled ? 'Collapse' : 'Expand' }}
            </button>
            <template v-if="groupedArrays.year[yearIndex].enabled">
              <div
                v-for="(monthVF, monthIndex) in yearVF"
                :key="monthIndex"
              >
                <div class="mt-1rem ml-2rem">
                  <span class="text-base font-bold mr-0_5rem ledger-underline">
                    {{ groupedArrays.year[yearIndex].year }}-{{ groupedArrays.month[yearIndex][monthIndex].month }}
                  </span>
                  <button
                    class="tb-c text-base toggle-link"
                    type="button"
                    @click="toggleElements($event, groupedArrays.year[yearIndex], groupedArrays.month[yearIndex][monthIndex])"
                  >
                    {{ groupedArrays.month[yearIndex][monthIndex].enabled ? 'Collapse' : 'Expand' }}
                  </button>
                  <template v-if="groupedArrays.month[yearIndex][monthIndex].enabled">
                    <div
                      v-for="(dayVF, dayIndex) in monthVF"
                      :key="dayIndex"
                    >
                      <div class="mt-1rem ml-2rem">
                        <span
                          v-if="viewByDate"
                          class="text-sm font-bold ledger-underline"
                        >
                          {{ groupedArrays.year[yearIndex].year }}-{{ groupedArrays.month[yearIndex][monthIndex].month }}-{{ groupedArrays.day[yearIndex][monthIndex][dayIndex].day }}
                        </span>
                        <button
                          v-if="viewByDate"
                          class="tb-c text-sm mr-0_5rem toggle-link"
                          type="button"
                          @click="toggleElements($event, groupedArrays.year[yearIndex], groupedArrays.month[yearIndex][monthIndex], groupedArrays.day[yearIndex][monthIndex][dayIndex])"
                        >
                          {{ groupedArrays.day[yearIndex][monthIndex][dayIndex].enabled ? 'Collapse' : 'Expand' }}
                        </button>
                        <div class="mt-1rem mission-grid">
                          <button
                            v-if="isDayRowVisible(yearIndex, monthIndex, dayIndex) && multiViewMode === 'row'"
                            :class="'hover:text-gray-600 rounded-md hover:ledger-underline text-xs font-bold btn btn-outline-dark bg-transparent mission-row-view' + (dayVF.length > 3 ? ' mission-row-view-longer' : '')"
                            @click="triggerRowView(yearIndex, monthIndex, dayIndex)"
                          >
                            <span class="text-xs">View row</span>
                          </button>
                          <div :class="'mission-grid ' + (multiViewMode === 'row' ? 'mission-items-view' : 'mission-items-full')">
                            <template v-if="isDayRowVisible(yearIndex, monthIndex, dayIndex)">
                            <div
                              v-for="(mission, missionIndex) in dayVF"
                              :key="missionIndex"
                              class="text-sm mission-item-3"
                              :data-missionid="mission.missionId"
                            >
                              <input
                                v-if="multiViewMode === 'free'"
                                type="checkbox"
                                class="ext-opt-check mr-0_5rem"
                                :value="mission"
                                :id="'multiViewMissionCb_' + mission.missionId"
                                :checked="multiViewFreeSelectIds.includes(mission.missionId)"
                                @change="(e) => { e.preventDefault(); handleMultiViewSelection(e, mission.missionId); }"
                              />
                              <button
                                class="text-sm mr-5 font-bold btn btn-outline-dark bg-transparent"
                                type="button"
                                @click="viewSpecificMission(mission.missionId)"
                              >
                                <span v-if="!viewByDate" :class="getShipColorText(mission)">
                                  {{
                                    groupedArrays.year[yearIndex].year + '-' +
                                    (groupedArrays.month[yearIndex][monthIndex].month < 10 ? '0' : '') + groupedArrays.month[yearIndex][monthIndex].month + '-' +
                                    (groupedArrays.day[yearIndex][monthIndex][dayIndex].day < 10 ? '0' : '') + groupedArrays.day[yearIndex][monthIndex][dayIndex].day
                                  }}
                                </span>
                                <span v-show="viewMissionTimes" :class="getShipColorText(mission, true)">
                                  {{ ' ' + hhmmss(ledgerDate(mission.launchDT)) }}
                                </span>
                                <span class="text-gray-400 ml-0_5rem">
                                  <span :class="'text-duration-' + mission.durationType">{{ mission.shipString }}</span>
                                  <span v-if="mission.level != null && mission.level > 0">
                                    ({{ mission.level }}<span class="text-goldenstar">&#9733;</span>)
                                  </span>
                                  <TargetDisplay :target="mission.target" />
                                </span>
                              </button>
                            </div>
                            </template>
                          </div>
                        </div>
                      </div>
                    </div>
                  </template>
                </div>
              </div>
            </template>
            <br />
            <hr
              v-if="groupedArrays.year[yearIndex].enabled && yearIndex !== groupedArrays.year.length - 1"
              class="pt-3rem pb-3rem invisible mt-1"
            />
          </template>
        </div>
      </div>
    </div>

    <!-- No data fallback -->
    <div
      v-if="!doesDataExist"
      class="text-center mt-1rem rounded-md border border-red-700 py-2"
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
import { useFetch } from '../composables/useFetch'
import type {
  DatabaseMission,
  MissionDrop,
  PossibleTarget,
  PossibleArtifact,
  PossibleMission,
} from '../types/bridge'
import FullFilter from '../components/FullFilter.vue'
import SearchOverSelector from '../components/SearchOverSelector.vue'
import ShipDisplay from '../components/ShipDisplay.vue'
import TargetDisplay from '../components/TargetDisplay.vue'

// ───────────────────────────────────────────────────────────────────────────────
// Shared state
// ───────────────────────────────────────────────────────────────────────────────

const { existingData, activeTab } = useAppState()
const { mennoDataLoaded, getMennoData, load: loadMennoData } = useMennoData()
const { isFetching } = useFetch()

// ───────────────────────────────────────────────────────────────────────────────
// Types local to this view
// ───────────────────────────────────────────────────────────────────────────────

interface FilterCondition {
  topLevel: string
  op: string
  val: string
}

interface GroupedYear {
  year: number
  enabled: boolean
}
interface GroupedMonth {
  month: number
  enabled: boolean
}
interface GroupedDay {
  day: number
  enabled: boolean
}
interface GroupedArrays {
  year: GroupedYear[]
  month: GroupedMonth[][]
  day: GroupedDay[][][]
}

interface FilterOption {
  text: string
  value: string
  styleClass?: string
  imagePath?: string
  rarity?: number
  rarityGif?: string
}

// A view-mission-data object - used by ShipDisplay.
// Matches ShipDisplay's ViewMissionData interface (extends LedgerData).
interface MennoConfigItem {
  artifactConfiguration: {
    artifactType: { id: number }
    artifactLevel: number
    artifactRarity: { id: number }
  }
  totalDrops: number
}
interface ViewMissionDataMennoData {
  configs: MennoConfigItem[]
  totalDropsCount: number
}
interface ViewMissionData {
  missionInfo: DatabaseMission & { targetInt?: number }
  artifacts: InnerDrop[]
  stones: InnerDrop[]
  stoneFragments: InnerDrop[]
  ingredients: InnerDrop[]
  launchDT: Date
  returnDT: Date
  durationStr: string
  capacityModifier: number | string
  prevMission: string | null
  nextMission: string | null
  missionCount?: number
  mennoData: ViewMissionDataMennoData
}

interface InnerDrop extends MissionDrop {
  count: number
  protoName?: string
  displayName?: string
}

// ───────────────────────────────────────────────────────────────────────────────
// Account selector state
// ───────────────────────────────────────────────────────────────────────────────

const selectedMissionAccount = ref<string | null>(null)
const viewMissionAccountSelectRef = ref<HTMLElement | null>(null)
const accountDropdownOpen = ref(false)
const eidMissionsBeingLoaded = ref(false)
const loadedEid = ref<string | null>(null)

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
  if (id != null && id !== '') selectedMissionAccount.value = id
  accountDropdownOpen.value = false
}

// ───────────────────────────────────────────────────────────────────────────────
// Mission list + filter result state
// ───────────────────────────────────────────────────────────────────────────────

const allLoadedMissions = ref<DatabaseMission[] | null>(null)
const filteredMissions = ref<DatabaseMission[] | null>(null)
const groupedArrays = ref<GroupedArrays>({ year: [], month: [], day: [] })
const allVisible = ref(true)

const resultsDiv = ref<HTMLElement | null>(null)

const hasAnyFilterableData = computed(() => {
  if (groupedMissions.value && Object.keys(groupedMissions.value).length > 0) return true
  if (
    filteredMissions.value != null &&
    allLoadedMissions.value != null &&
    filteredMissions.value.length !== allLoadedMissions.value.length
  ) return true
  return false
})

// groupedArrays is updated alongside groupedMissions to reset expand-collapse state
// when filters change. The three assignments below are intentional side effects.
const groupedMissions = computed(() => {
  const fm = filteredMissions.value
  if (fm == null || fm.length === 0) return [] as DatabaseMission[][][][]

  const uniqueYears = [...new Set(fm.map((mission) => ledgerDate(mission.launchDT).getFullYear()))].reverse()
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  groupedArrays.value.year = uniqueYears.map((year) => ({ year, enabled: true }))

  const uniqueMonthsArr = uniqueYears.map((year) =>
    [...new Set(
      fm
        .filter((mission) => ledgerDate(mission.launchDT).getFullYear() === year)
        .map((mission) => ledgerDate(mission.launchDT).getMonth() + 1),
    )].reverse(),
  )
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  groupedArrays.value.month = uniqueMonthsArr.map((months) =>
    months.map((month) => ({ month, enabled: true })),
  )

  const uniqueDaysArr = uniqueYears.map((year, yearIndex) =>
    uniqueMonthsArr[yearIndex].map((month) =>
      [...new Set(
        fm
          .filter((mission) => {
            const d = ledgerDate(mission.launchDT)
            return d.getFullYear() === year && d.getMonth() === month - 1
          })
          .map((mission) => ledgerDate(mission.launchDT).getDate()),
      )].reverse(),
    ),
  )
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  groupedArrays.value.day = uniqueDaysArr.map((year) =>
    year.map((month) => month.map((day) => ({ day, enabled: true }))),
  )

  const missionsForDay = (year: number, month: number, day: number) =>
    fm
      .filter((mission) => {
        const d = ledgerDate(mission.launchDT)
        return d.getFullYear() === year && d.getMonth() === month - 1 && d.getDate() === day
      })
      .reverse()

  return uniqueYears.map((year, yearIndex) =>
    uniqueMonthsArr[yearIndex].map((month, monthIndex) =>
      uniqueDaysArr[yearIndex][monthIndex].map((day) => missionsForDay(year, month, day)),
    ),
  )
})

function isDayRowVisible(yearIndex: number, monthIndex: number, dayIndex: number): boolean {
  if (viewByDate.value) return groupedArrays.value.day[yearIndex][monthIndex][dayIndex].enabled
  return groupedArrays.value.month[yearIndex][monthIndex].enabled
}

// eslint-disable-next-line sonarjs/cognitive-complexity
function toggleElements(
  _event: Event,
  passedYear?: GroupedYear,
  passedMonth?: GroupedMonth,
  passedDay?: GroupedDay,
) {
  const yA = groupedArrays.value.year
  const mA = groupedArrays.value.month
  const dA = groupedArrays.value.day
  if (passedYear) {
    const yearObj = yA[yA.indexOf(passedYear)]
    if (passedMonth) {
      const yIdx = yA.indexOf(passedYear)
      const monthObj = mA[yIdx][mA[yIdx].indexOf(passedMonth)]
      if (passedDay) {
        const mIdx = mA[yIdx].indexOf(passedMonth)
        const dayObj = dA[yIdx][mIdx][dA[yIdx][mIdx].indexOf(passedDay)]
        dayObj.enabled = !dayObj.enabled
      } else monthObj.enabled = !monthObj.enabled
    } else yearObj.enabled = !yearObj.enabled
  } else {
    allVisible.value = !allVisible.value
    for (let yi = 0; yi < yA.length; yi++) {
      yA[yi].enabled = allVisible.value
      for (let mi = 0; mi < mA[yi].length; mi++) {
        mA[yi][mi].enabled = allVisible.value
        for (const day of dA[yi][mi]) {
          day.enabled = allVisible.value
        }
      }
    }
  }
}

// ───────────────────────────────────────────────────────────────────────────────
// Viewing option state
// ───────────────────────────────────────────────────────────────────────────────

const viewByDate = ref(false)
const viewMissionTimes = ref(true)
const recolorDC = ref(false)
const recolorBC = ref(false)
const showExpectedDropsPerShip = ref(true)
const hideOptions = ref(false)
const hideFilter = ref(false)
const multiViewMode = ref<'off' | 'row' | 'free'>('off')
const viewMissionSortMethod = ref<'default' | 'iv'>('default')

watch(mennoDataLoaded, () => {
  if (!mennoDataLoaded.value) showExpectedDropsPerShip.value = false
})

function toggleOptions(event: Event) {
  event.preventDefault()
  hideOptions.value = !hideOptions.value
}
function toggleFilter(event: Event) {
  event.preventDefault()
  hideFilter.value = !hideFilter.value
}

function getShipColorText(missionData: DatabaseMission, altMode = false): string {
  if (missionBeingViewed.value === missionData.missionId) {
    return altMode ? 'text-selectedmissiondarker' : 'text-selectedmission'
  }
  if (recolorBC.value && missionData.isBuggedCap) {
    return altMode ? 'text-buggedcapdarker' : 'text-buggedcap'
  }
  if (recolorDC.value && missionData.isDubCap) {
    return altMode ? 'text-dubcapdarker' : 'text-dubcap'
  }
  return altMode ? 'text-gray-500' : 'text-gray-400'
}

// ───────────────────────────────────────────────────────────────────────────────
// Filter state
// ───────────────────────────────────────────────────────────────────────────────

const filterApplyTime = ref('')
const filterConditionsCount = ref(1)
const topLevelFilterOptions = ref<(string | null)[]>([])
const filterOperators = ref<(string | null)[]>([])
const filterValues = ref<(string | null)[]>([])

const orFilterConditionsCount = ref<(number | null)[]>([])
const orFilterTopLevel = ref<(string | null)[][]>([[]])
const orFilterOperators = ref<(string | null)[][]>([[]])
const orFilterValues = ref<(string | null)[][]>([[]])

const dFilterValues = ref<(string | null)[]>([])
const dOrFilterValues = ref<(string | null)[][]>([[]])

const dataFilter = ref<FilterCondition[]>([])
const orDataFilter = ref<FilterCondition[][]>([[]])
const filterHasChanged = ref(false)
const dataBeingFiltered = ref(false)

const filterWarningRead = ref(false)
const hideFilterWarning = ref(false)

async function dismissFilterWarning() {
  await globalThis.setFilterWarningRead(true)
  filterWarningRead.value = true
  hideFilterWarning.value = true
}

watch(filterValues, () => {
  dFilterValues.value = filterValues.value.map(
    (_f, index) => (dFilterValues.value[index] === undefined ? '' : dFilterValues.value[index]),
  )
}, { deep: true })

watch(orFilterValues, () => {
  dOrFilterValues.value = orFilterValues.value.map((orFilter, index) =>
    dOrFilterValues.value[index] === undefined
      ? []
      : dOrFilterValues.value[index].slice(0, orFilter?.length ?? 0),
  )
}, { deep: true })

watch([dataFilter, orDataFilter], () => {
  filterHasChanged.value = true
})

// ───────────────────────────────────────────────────────────────────────────────
// Possible targets / artifact configs (for filter value options)
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
    case 1: return 'images/rare.gif'
    case 2: return 'images/epic.gif'
    case 3: return 'images/legendary.gif'
    default: return ''
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
// Filter handling
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
      if (intOrIndex == null) topLevelFilterOptions.value[intIndex] = value
      else orFilterTopLevel.value[intIndex][intOrIndex] = value
      break
    case 'operator':
      if (intOrIndex == null) filterOperators.value[intIndex] = value
      else orFilterOperators.value[intIndex][intOrIndex] = value
      break
    case 'value':
      if (intOrIndex == null) filterValues.value[intIndex] = value
      else orFilterValues.value[intIndex][intOrIndex] = value
      break
  }
}

// eslint-disable-next-line sonarjs/cognitive-complexity
function updateValueStyling(passedEl: HTMLElement, isOr: boolean) {
  const idParts = passedEl.id.split('-')
  const index = Number.parseInt(idParts[2])
  const orIndex = isOr ? Number.parseInt(idParts[idParts.length - 1]) : null
  if (isOr && orIndex == null) return
  const el = document.getElementById('filter-value-' + index + (isOr ? '-' + orIndex : ''))
  if (!el) return
  const classMatchBorder = el.className.match(/ border-(.*?)(\s|$)/)
  const classMatchText = el.className.replace('text-sm', ' ').match(/ text-(.*?)(\s|$)/)
  const existingBorder = classMatchBorder ? 'border-' + classMatchBorder[1].trim() : ''
  const existingText = classMatchText ? 'text-' + classMatchText[1].trim() : ''
  const isDrops = isOr
    ? orFilterTopLevel.value[index][orIndex!] === 'drops'
    : topLevelFilterOptions.value[index] === 'drops'
  const dropsValue = isOr ? orFilterValues.value[index][orIndex!] : filterValues.value[index]
  const isDuration = isOr
    ? orFilterTopLevel.value[index][orIndex!] === 'duration'
    : topLevelFilterOptions.value[index] === 'duration'
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
  filterHasChanged.value = true
  const newDataFilter: FilterCondition[] = []
  if (topLevelFilterOptions.value.length !== 0 && topLevelFilterOptions.value != null) {
    for (let i = 0; i < topLevelFilterOptions.value.length; i++) {
      const topLevel = topLevelFilterOptions.value[i]
      const op = filterOperators.value[i]
      const val = filterValues.value[i]
      if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) break
      newDataFilter.push({ topLevel, op, val })
    }
  }
  dataFilter.value = newDataFilter

  const newOrDataFilter: FilterCondition[][] = []
  for (let i = 0; i < orFilterConditionsCount.value.length; i++) {
    const arr: FilterCondition[] = []
    if (orFilterTopLevel.value[i] == null || orFilterOperators.value[i] == null || orFilterValues.value[i] == null) continue
    for (let j = 0; j < orFilterTopLevel.value[i].length; j++) {
      const topLevel = orFilterTopLevel.value[i][j]
      const op = orFilterOperators.value[i][j]
      const val = orFilterValues.value[i][j]
      if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) break
      arr.push({ topLevel, op, val })
    }
    newOrDataFilter.push(arr)
  }
  orDataFilter.value = newOrDataFilter
}

function updateFilterConditionsCount() {
  filterHasChanged.value = true
  const newDataFilter: FilterCondition[] = []
  if (topLevelFilterOptions.value.length === 0 || topLevelFilterOptions.value == null) return
  let maxIndex = 0
  let set = false

  for (let i = 0; i < topLevelFilterOptions.value.length; i++) {
    const topLevel = topLevelFilterOptions.value[i]
    const op = filterOperators.value[i]
    const val = filterValues.value[i]

    if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) {
      maxIndex = i
      break
    }

    newDataFilter.push({ topLevel, op, val })

    if (i + 1 === filterConditionsCount.value) {
      filterConditionsCount.value += 1
      set = true
    }
  }
  if (maxIndex === 0) {
    maxIndex = topLevelFilterOptions.value.filter((f) => f != null && f !== '').length
  }

  if (!set) filterConditionsCount.value = maxIndex + 1
  dataFilter.value = newDataFilter
}

interface HandleFilterChangeEventTarget extends HTMLElement {
  oldValue?: string | null
  value?: string
  type?: string
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
    const index = Number.parseInt(targetId.split('-').pop() ?? '0')
    const options = getFilterValueOptions(targetEl.value ?? '')
    const opts = new Set(options.map((o) => String(o.value)))
    if (!opts.has(String(filterOperators.value[index]))) filterOperators.value[index] = null
    if (!opts.has(String(filterValues.value[index]))) {
      filterValues.value[index] = null
      dFilterValues.value[index] = null
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
  const idParts = targetId.split('-')
  const index = Number.parseInt(idParts[2])
  const orIndex = Number.parseInt(idParts[idParts.length - 1])
  updateFilterNoReCount()
  updateValueStyling(targetEl, true)
  if (targetId.includes('filter-top')) {
    const options = getFilterValueOptions(targetEl.value ?? '')
    const opts = new Set(options.map((o) => String(o.value)))
    if (!opts.has(String(orFilterOperators.value[index][orIndex]))) {
      orFilterOperators.value[index][orIndex] = null
    }
    if (!opts.has(String(orFilterValues.value[index][orIndex]))) {
      orFilterValues.value[index][orIndex] = null
      dOrFilterValues.value[index][orIndex] = null
    }
  }
}

function addOr(index: number) {
  if (orFilterConditionsCount.value == null) orFilterConditionsCount.value = []
  if (orFilterConditionsCount.value[index] == null) orFilterConditionsCount.value[index] = 1
  else orFilterConditionsCount.value[index] = (orFilterConditionsCount.value[index] as number) + 1

  if (orFilterTopLevel.value[index] == null) orFilterTopLevel.value[index] = []
  if (orFilterOperators.value[index] == null) orFilterOperators.value[index] = []
  if (orFilterValues.value[index] == null) orFilterValues.value[index] = []

  orFilterTopLevel.value[index].push(null)
  orFilterOperators.value[index].push(null)
  orFilterValues.value[index].push(null)
}

function removeAndShift(index: number) {
  for (let i = index; i < topLevelFilterOptions.value.length; i++) {
    topLevelFilterOptions.value[i] = topLevelFilterOptions.value[i + 1]
    filterOperators.value[i] = filterOperators.value[i + 1]
    filterValues.value[i] = filterValues.value[i + 1]
    dFilterValues.value[i] = dFilterValues.value[i + 1]

    orFilterConditionsCount.value[i] = orFilterConditionsCount.value[i + 1]
    orFilterTopLevel.value[i] = orFilterTopLevel.value[i + 1]
    orFilterOperators.value[i] = orFilterOperators.value[i + 1]
    orFilterValues.value[i] = orFilterValues.value[i + 1]
    dOrFilterValues.value[i] = dOrFilterValues.value[i + 1]
  }
  updateFilterConditionsCount()
  updateFilterNoReCount()
  filterHasChanged.value = true

  setTimeout(() => {
    document.querySelectorAll("[id^='filter-top-']").forEach((el) =>
      updateValueStyling(el as HTMLElement, false),
    )
  }, 10)
}

function removeOrAndShift(index: number, orIndex: number) {
  for (let i = orIndex; i < orFilterTopLevel.value[index].length; i++) {
    orFilterTopLevel.value[index][i] = orFilterTopLevel.value[index][i + 1]
    orFilterOperators.value[index][i] = orFilterOperators.value[index][i + 1]
    orFilterValues.value[index][i] = orFilterValues.value[index][i + 1]
    dOrFilterValues.value[index][i] = dOrFilterValues.value[index][i + 1]
  }
  const cur = orFilterConditionsCount.value[index]
  if (cur != null) {
    const next = cur - 1
    orFilterConditionsCount.value[index] = next === 0 ? null : next
  }
  updateFilterNoReCount()
  filterHasChanged.value = true
}

function generateFilterConditionsArr() {
  return new Array(filterConditionsCount.value)
}

function filterModVals() {
  return {
    top: topLevelFilterOptions.value,
    operator: filterOperators.value,
    value: filterValues.value,
    dValue: dFilterValues.value,
    orTop: orFilterTopLevel.value,
    orOperator: orFilterOperators.value,
    orValue: orFilterValues.value,
    orDValue: dOrFilterValues.value,
    orCount: orFilterConditionsCount.value,
  }
}

// ───────────────────────────────────────────────────────────────────────────────
// Apply filter logic
// ───────────────────────────────────────────────────────────────────────────────

function ledgerDate(timestamp: number): Date {
  return new Date(timestamp * 1000)
}
function ledgerDateObj(date: string): Date {
  const parts = date.split('-')
  return new Date(Number.parseInt(parts[0]), Number.parseInt(parts[1]) - 1, Number.parseInt(parts[2]))
}
function hhmmss(date: Date): string {
  return `${date.getHours().toString().padStart(2, '0')}:${date
    .getMinutes()
    .toString()
    .padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
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
        const allDrops = await globalThis.getShipDrops(loadedEid.value ?? '', mission.missionId)
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

// ───────────────────────────────────────────────────────────────────────────────
// Drop / Target filter overlay state
// ───────────────────────────────────────────────────────────────────────────────

const dropSelectList = ref<FilterOption[]>([])
const dropFilterSelectedIndex = ref<number | null>(null)
const dropFilterSelectedOrIndex = ref<number | null>(null)
const dropFilterMenuOpen = ref(false)
const dropSearchTerm = ref('')

const targetSelectList = ref<FilterOption[]>([])
const targetFilterSelectedIndex = ref<number | null>(null)
const targetFilterSelectedOrIndex = ref<number | null>(null)
const targetFilterMenuOpen = ref(false)
const targetSearchTerm = ref('')

watch(dropSearchTerm, () => {
  dropSelectList.value = getFilterValueOptions('drops').filter((drop) =>
    drop.text.toLowerCase().includes(dropSearchTerm.value.toLowerCase()),
  )
})
watch(targetSearchTerm, () => {
  targetSelectList.value = getFilterValueOptions('target').filter((target) =>
    target.text.toLowerCase().includes(targetSearchTerm.value.toLowerCase()),
  )
})

function openDropFilterMenu(index: number, orIndex: number | null) {
  dropSelectList.value = getFilterValueOptions('drops')
  dropFilterSelectedIndex.value = index
  dropFilterSelectedOrIndex.value = orIndex ?? null
  dropFilterMenuOpen.value = true
  const el = document.querySelector('.overlay-drop') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
  const input = document.getElementById('drop-search') as HTMLInputElement | null
  input?.focus()
}
function closeDropFilterMenu() {
  dropSearchTerm.value = ''
  dropFilterSelectedIndex.value = null
  dropFilterSelectedOrIndex.value = null
  dropFilterMenuOpen.value = false
  const el = document.querySelector('.overlay-drop') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
}
function selectDropFilter(drop: FilterOption) {
  const newValue = drop.value.toString().split('.')[0]
  if (dropFilterSelectedIndex.value != null) {
    if (dropFilterSelectedOrIndex.value == null) {
      filterValues.value[dropFilterSelectedIndex.value] = newValue
      dFilterValues.value[dropFilterSelectedIndex.value] = drop.text
      handleFilterChange(`filter-value-${dropFilterSelectedIndex.value}`)
      const el = document.getElementById(`filter-value-${dropFilterSelectedIndex.value}`) as HTMLInputElement | null
      if (el) el.size = drop.text.length
    } else {
      orFilterValues.value[dropFilterSelectedIndex.value][dropFilterSelectedOrIndex.value] = newValue
      dOrFilterValues.value[dropFilterSelectedIndex.value][dropFilterSelectedOrIndex.value] = drop.text
      handleOrFilterChange(`filter-value-${dropFilterSelectedIndex.value}-${dropFilterSelectedOrIndex.value}`)
      const el = document.getElementById(
        `filter-value-${dropFilterSelectedIndex.value}-${dropFilterSelectedOrIndex.value}`,
      ) as HTMLInputElement | null
      if (el) el.size = drop.text.length
    }
  }
  closeDropFilterMenu()
}

function openTargetFilterMenu(index: number, orIndex: number | null) {
  targetSelectList.value = getFilterValueOptions('target')
  targetFilterSelectedIndex.value = index
  targetFilterSelectedOrIndex.value = orIndex ?? null
  targetFilterMenuOpen.value = true
  const el = document.querySelector('.overlay-target') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
  const input = document.getElementById('target-search') as HTMLInputElement | null
  input?.focus()
}
function closeTargetFilterMenu() {
  targetSearchTerm.value = ''
  targetFilterSelectedIndex.value = null
  targetFilterSelectedOrIndex.value = null
  targetFilterMenuOpen.value = false
  const el = document.querySelector('.overlay-target') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
}
function selectTargetFilter(target: FilterOption) {
  const newValue = target.value.toString().split('.')[0]
  if (targetFilterSelectedIndex.value != null) {
    if (targetFilterSelectedOrIndex.value == null) {
      filterValues.value[targetFilterSelectedIndex.value] = newValue
      dFilterValues.value[targetFilterSelectedIndex.value] = target.text
      handleFilterChange(`filter-value-${targetFilterSelectedIndex.value}`)
      const el = document.getElementById(
        `filter-value-${targetFilterSelectedIndex.value}`,
      ) as HTMLInputElement | null
      if (el) el.size = target.text.length
    } else {
      orFilterValues.value[targetFilterSelectedIndex.value][targetFilterSelectedOrIndex.value] = newValue
      dOrFilterValues.value[targetFilterSelectedIndex.value][targetFilterSelectedOrIndex.value] = target.text
      handleOrFilterChange(
        `filter-value-${targetFilterSelectedIndex.value}-${targetFilterSelectedOrIndex.value}`,
      )
      const el = document.getElementById(
        `filter-value-${targetFilterSelectedIndex.value}-${targetFilterSelectedOrIndex.value}`,
      ) as HTMLInputElement | null
      if (el) el.size = target.text.length
    }
  }
  closeTargetFilterMenu()
}

// ───────────────────────────────────────────────────────────────────────────────
// Mission view overlay state (single and multi)
// ───────────────────────────────────────────────────────────────────────────────

const viewMissionData = ref<ViewMissionData | null>(null)
const missionBeingViewed = ref<string | null>(null)
const boolMissionBeingViewed = ref(false)

const multiViewMissionData = ref<ViewMissionData[]>([])
const missionsBeingViewed = ref<string[]>([])
const rowViewBeingLoaded = ref(false)
const multiViewFreeSelectIds = ref<string[]>([])

function closeMissionOverlay() {
  const el = document.querySelector('.overlay-mission') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
  viewMissionData.value = null
  missionBeingViewed.value = null
  boolMissionBeingViewed.value = false
}
function openMissionOverlay() {
  const el = document.querySelector('.overlay-mission') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
  boolMissionBeingViewed.value = true
}
function closeMultiMissionOverlay() {
  const el = document.querySelector('.overlay-multi-mission') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
  multiViewMissionData.value = []
  missionsBeingViewed.value = []
}
function openMultiMissionOverlay() {
  const el = document.querySelector('.overlay-multi-mission') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
}

function handleKeyDown(event: KeyboardEvent) {
  if (!boolMissionBeingViewed.value) return
  event.preventDefault()
  event.stopPropagation()
  const data = viewMissionData.value
  if (!data) return
  if (event.key === 'ArrowLeft' && data.prevMission) {
    viewSpecificMission(data.prevMission as string)
  } else if (event.key === 'ArrowRight' && data.nextMission) {
    viewSpecificMission(data.nextMission as string)
  }
}
watch(boolMissionBeingViewed, () => {
  if (boolMissionBeingViewed.value) globalThis.addEventListener('keydown', handleKeyDown)
  else globalThis.removeEventListener('keydown', handleKeyDown)
})

// ───────────────────────────────────────────────────────────────────────────────
// Sorting helpers for mission drops
// ───────────────────────────────────────────────────────────────────────────────

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

function groupedSpecType(collection: DropLike[]): Record<string, DropLike> {
  return collection.reduce((acc, obj) => {
    const key = obj.name + '_' + obj.level + '_' + obj.specType + '_' + obj.rarity
    if (acc[key]) {
      (acc[key].count as number)++
    } else {
      acc[key] = obj
      acc[key].count = 1
    }
    return acc
  }, {} as Record<string, DropLike>)
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
function sortedGroupedSpecType<T extends DropLike>(collection: T[]): T[] {
  return sortGroupAlreadyCombed(Object.values(groupedSpecType(collection as unknown as DropLike[])) as T[])
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

// ───────────────────────────────────────────────────────────────────────────────
// Fetch mission + view logic
// ───────────────────────────────────────────────────────────────────────────────

async function onViewSubmit(event: Event) {
  event.preventDefault()
  for (const _ of topLevelFilterOptions.value) removeAndShift(0)
  await doViewMissionsOfEid()
}

async function doViewMissionsOfEid() {
  if (!selectedMissionAccount.value) return
  filterApplyTime.value = ''
  multiViewFreeSelectIds.value = []
  eidMissionsBeingLoaded.value = true
  allLoadedMissions.value = await globalThis.viewMissionsOfEid(selectedMissionAccount.value)
  filteredMissions.value = allLoadedMissions.value
  loadedEid.value = selectedMissionAccount.value
  eidMissionsBeingLoaded.value = false
}

async function getSpecificMissionData(eid: string, missionId: string, extendedInfo: boolean): Promise<ViewMissionData | null> {
  const missionInfo = await globalThis.getMissionInfo(eid, missionId)
  if (missionInfo.ship == null || missionInfo.ship < 0 || !missionInfo.missionId) return null
  const allDrops = await globalThis.getShipDrops(eid, missionId)
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

async function viewSpecificMission(missionId: string, returnValues = false): Promise<ViewMissionData | false> {
  if (isFetching.value) return false
  const newMissionViewData = await getSpecificMissionData(loadedEid.value ?? '', missionId, true)
  if (newMissionViewData == null) return false
  const sortFn =
    viewMissionSortMethod.value === 'iv' ? inventoryVisualizerSort : sortGroupAlreadyCombed

  const mi = newMissionViewData.missionInfo as (DatabaseMission & { targetInt?: number }) | undefined
  if (mi) {
    const mennoTargetInt = mi.targetInt === -1 ? 1000 : (mi.targetInt ?? 1000)
    const mennoShip = mi.ship
    const mennoDuration = mi.durationType
    const mennoLevel = mi.level
    const mennoConfigItems = await getMennoData(mennoShip, mennoDuration, mennoLevel, mennoTargetInt)
    if (mennoConfigItems) {
      newMissionViewData.mennoData = {
        totalDropsCount: mennoConfigItems.reduce(
          (acc: number, cur: { totalDrops: number }) => acc + cur.totalDrops,
          0,
        ),
        configs: mennoConfigItems as unknown as MennoConfigItem[],
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
  openMultiMissionOverlay()
  missionsBeingViewed.value = []
  multiViewMissionData.value = []
  for (const missionId of missionIds) {
    const data = await viewSpecificMission(missionId, true)
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

// ───────────────────────────────────────────────────────────────────────────────
// Lifecycle
// ───────────────────────────────────────────────────────────────────────────────

onMounted(async () => {
  filterWarningRead.value = (await globalThis.filterWarningRead()) ?? false

  artifactConfigs.value = await globalThis.getAfxConfigs()
  maxQuality.value = await globalThis.getMaxQuality()
  durationConfigs.value = await globalThis.getDurationConfigs()
  possibleTargets.value = await globalThis.getPossibleTargets()

  await loadMennoData()

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
