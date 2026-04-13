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
    <div v-show="boolMissionBeingViewed" class="top-click-detect mission-view-overlay overlay-mission">
      <div class="inner-click-detect max-w-70vw max-h-90vh overflow-auto bg-dark rounded-lg relative p-1rem" @click.stop>
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
    <div v-show="multiMissionOverlayOpen" class="top-click-detect mission-view-overlay overlay-multi-mission">
      <div class="inner-click-detect max-w-90vw max-h-90vh bg-dark rounded-lg relative p-1rem" @click.stop>
        <button class="detect-trigger hover:text-gray-500 close-button" @click="closeMultiMissionOverlay">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
        <div v-if="rowViewBeingLoaded" class="text-center px-8 py-4 min-w-60">
          <div class="mt-0_5rem text-center text-xl text-gray-400">Multi-Mission View Loading</div>
          <hr class="mt-1rem mb-1rem w-full" />
          <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
          <div class="mt-3 text-sm text-gray-400">
            Loading mission
            <span class="text-gray-200 font-semibold">{{ missionsBeingViewed.length + 1 }}</span>
            of
            <span class="text-gray-200 font-semibold">{{ multiViewTotalToLoad }}</span>
          </div>
          <div class="mt-2 w-full bg-darker rounded-full h-1.5 overflow-hidden">
            <div
              class="bg-blue-500 h-1.5 rounded-full transition-all duration-300"
              :style="{ width: multiViewTotalToLoad > 0 ? ((missionsBeingViewed.length / multiViewTotalToLoad) * 100) + '%' : '0%' }"
            />
          </div>
        </div>
        <div v-else class="max-w-90vw max-h-80vh overflow-auto">
          <div class="flex justify-center gap-2 mb-3">
            <button
              type="button"
              :class="multiViewDisplayMode === 'separate' ? 'text-blue-400 ledger-underline font-semibold' : 'text-gray-500 hover:text-gray-300'"
              class="text-sm"
              @click="multiViewDisplayMode = 'separate'"
            >Separate</button>
            <span class="text-gray-600 text-sm">|</span>
            <button
              type="button"
              :class="multiViewDisplayMode === 'combined' ? 'text-blue-400 ledger-underline font-semibold' : 'text-gray-500 hover:text-gray-300'"
              class="text-sm"
              @click="multiViewDisplayMode = 'combined'"
            >Combined</button>
          </div>

          <!-- Separate mode: side-by-side columns -->
          <div v-if="multiViewDisplayMode === 'separate'" class="flex justify-between gap-2 mb-4">
            <div
              v-for="(missionData, missionDataIndex) in multiViewMissionData"
              :key="missionDataIndex"
              :class="'w-1/' + multiViewMissionData.length"
              class="bg-darkerer rounded-lg py-2"
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

          <!-- Combined mode: merged drop pool with ship summary -->
          <div v-else class="px-7rem text-gray-300 text-center">
            <div class="flex flex-wrap justify-center gap-x-3 gap-y-2 mb-3 text-xs">
              <div
                v-for="(missionData, i) in multiViewMissionData"
                :key="i"
                class="flex flex-col items-center bg-darkerer rounded px-2 py-1"
              >
                <div>
                  <span :class="'text-duration-' + missionData.missionInfo.durationType">{{ missionData.missionInfo.shipString }}</span>
                  <span v-if="missionData.missionInfo.level > 0" class="text-goldenstar ml-1">{{ '★'.repeat(missionData.missionInfo.level) }}</span>
                </div>
                <div
                  v-if="missionData.missionInfo.target && missionData.missionInfo.target.toUpperCase() !== 'UNKNOWN'"
                  class="text-gray-400"
                >
                  {{ properCaseTarget(missionData.missionInfo.target) }}
                </div>
                <div class="text-gray-500">
                  {{ missionData.missionInfo.capacity }}
                  <span v-if="missionData.missionInfo.isBuggedCap" class="text-buggedcap ml-1">0.6x</span>
                  <span v-else-if="missionData.missionInfo.isDubCap" class="text-dubcap ml-1">{{ missionData.capacityModifier }}x</span>
                </div>
              </div>
            </div>
            <drop-display-container
              ledger-type="mission"
              :data="combinedMissionDropData"
              :show-expected-drops="false"
            />
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
          <span class="whitespace-pre"><template v-if="screenshotSafety">EI<span class="inline-block rounded-sm bg-current select-none" style="width: 16ch; height: 0.8em; vertical-align: -0.05em;"></span></template><template v-else>{{ accountById(selectedMissionAccount)?.id }}</template></span>
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
          class="ledger-list"
          tabindex="-1"
        >
          <li
            v-for="account in objectedExistingData"
            :key="account.id"
            class="drop-opt"
            @click="closeAccountDropdown(account.id)"
          >
            <template v-if="screenshotSafety">EI<span class="inline-block rounded-sm bg-current select-none" style="width: 16ch; height: 0.8em; vertical-align: -0.05em;"></span></template><template v-else>{{ account.id }}</template>
            (<span :style="'color: #' + account.accountColor">{{ account.nickname }} {{ account.ebString }}</span>
            - {{ account.missionCount }} missions)
          </li>
        </ul>
      </div>
      <button
        class="view-form-button"
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
      v-if="doesDataExist"
      class="min-h-7 max-h-50 px-2 py-2 text-sm text-gray-400 bg-darkest rounded-md tabular-nums overflow-auto mt-0_75rem"
    >
      <div v-if="hasAnyFilterableData">
        <div>
          <span class="mr-0_5rem section-heading">Options</span>
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
            <span class="section-heading">Multi-View Method</span><br />
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
            <span class="section-heading">Drops Sort Method</span><br />
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
            </span>
            <hr class="mb-0_5rem w-full" />
            <div v-if="multiViewMode === 'free' && multiViewFreeSelectIds.length > 0" class="flex items-center gap-2 mb-2">
              <button
                type="button"
                class="px-3 py-1 rounded-md border border-green-700 text-sm font-medium text-green-400 bg-transparent hover:bg-green-950/50"
                @click="triggerRowView()"
              >
                View Selected ({{ multiViewFreeSelectIds.length }})
              </button>
              <button
                type="button"
                class="px-3 py-1 rounded-md border border-gray-600 text-sm font-medium text-gray-400 bg-transparent hover:bg-darker"
                @click="multiViewFreeSelectIds = []"
              >
                Deselect All
              </button>
            </div>
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
    <NoDataFallback v-if="!doesDataExist" @navigate="activeTab = $event" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useAppState } from '../composables/useAppState'
import { useMennoData } from '../composables/useMennoData'
import { useFetch } from '../composables/useFetch'
import { useFilters } from '../composables/useFilters'
import { useDropdownSelector } from '../composables/useDropdownSelector'
import { screenshotSafety, collapseOlderSections } from '../composables/useSettings'
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
import DropDisplayContainer from '../components/DropDisplayContainer.vue'
import TargetDisplay from '../components/TargetDisplay.vue'
import NoDataFallback from '../components/NoDataFallback.vue'
import SegmentedProgressBar, { type ProgressSegment } from '../components/SegmentedProgressBar.vue'

// Shared state

const { existingData, activeTab } = useAppState()
const { mennoDataLoaded, getMennoData, load: loadMennoData } = useMennoData()
const { isFetching } = useFetch()

// Types local to this view

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

const {
  containerRef: viewMissionAccountSelectRef,
  isOpen: accountDropdownOpen,
  open: openAccountDropdown,
  close: closeAccountDropdown,
} = useDropdownSelector((id) => { selectedMissionAccount.value = id })
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

// Mission list + filter result state
// ───────────────────────────────────────────────────────────────────────────────

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
const groupedArrays = ref<GroupedArrays>({ year: [], month: [], day: [] })
const allVisible = ref(true)

const resultsDiv = ref<HTMLElement | null>(null)

const tabFilteredMissions = computed(() => {
  if (missionTypeTab.value === null || filteredMissions.value === null) return filteredMissions.value
  return filteredMissions.value.filter((m) => m.missionType === missionTypeTab.value)
})

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
// when filters change. The assignment below is an intentional side effect.
// Single O(N) pass: group missions year → month → day rather than repeated filter scans.
const groupedMissions = computed(() => {
  const fm = tabFilteredMissions.value
  if (fm == null || fm.length === 0) return [] as DatabaseMission[][][][]

  // Build a nested Map: year → month → day → missions[] in one pass
  const dateMap = new Map<number, Map<number, Map<number, DatabaseMission[]>>>()
  for (const mission of fm) {
    const d = ledgerDate(mission.launchDT)
    const y = d.getFullYear()
    const mo = d.getMonth() + 1
    const da = d.getDate()
    if (!dateMap.has(y)) dateMap.set(y, new Map())
    const ym = dateMap.get(y)!
    if (!ym.has(mo)) ym.set(mo, new Map())
    const md = ym.get(mo)!
    if (!md.has(da)) md.set(da, [])
    md.get(da)!.push(mission)
  }

  const uniqueYears = [...dateMap.keys()].sort((a, b) => b - a)
  const uniqueMonthsArr = uniqueYears.map((y) => [...dateMap.get(y)!.keys()].sort((a, b) => b - a))
  const uniqueDaysArr = uniqueYears.map((y, yi) =>
    uniqueMonthsArr[yi].map((mo) => [...dateMap.get(y)!.get(mo)!.keys()].sort((a, b) => b - a)),
  )

  const collapse = collapseOlderSections.value
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  groupedArrays.value.year = uniqueYears.map((year, yi) => ({ year, enabled: !collapse || yi === 0 }))
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  groupedArrays.value.month = uniqueMonthsArr.map((months, yi) =>
    months.map((month) => ({ month, enabled: !collapse || yi === 0 })),
  )
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  groupedArrays.value.day = uniqueDaysArr.map((year, yi) =>
    year.map((month) => month.map((day) => ({ day, enabled: !collapse || yi === 0 }))),
  )
  // eslint-disable-next-line vue/no-side-effects-in-computed-properties
  allVisible.value = !collapse || uniqueYears.length <= 1

  return uniqueYears.map((y, yi) =>
    uniqueMonthsArr[yi].map((mo, mi) =>
      uniqueDaysArr[yi][mi].map((da) => [...dateMap.get(y)!.get(mo)!.get(da)!].reverse()),
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

// Artifact/mission configs (loaded in onMounted, passed to useFilters)

const possibleTargets = ref<PossibleTarget[]>([])
const maxQuality = ref<number>(0)
const artifactConfigs = ref<PossibleArtifact[]>([])
const durationConfigs = ref<PossibleMission[]>([])

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

function hhmmss(date: Date): string {
  return `${date.getHours().toString().padStart(2, '0')}:${date
    .getMinutes()
    .toString()
    .padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
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
const multiMissionOverlayOpen = ref(false)
const multiViewDisplayMode = ref<'separate' | 'combined'>('separate')
const multiViewTotalToLoad = ref(0)

function mergeDropArrays(arrays: InnerDrop[][]): InnerDrop[] {
  const map = new Map<string, InnerDrop>()
  for (const arr of arrays) {
    for (const item of arr) {
      const key = `${item.id}_${item.level}_${item.rarity}`
      const existing = map.get(key)
      if (existing) {
        existing.count += item.count
      } else {
        map.set(key, { ...item })
      }
    }
  }
  return Array.from(map.values())
}

const combinedMissionDropData = computed(() => ({
  artifacts: mergeDropArrays(multiViewMissionData.value.map((m) => m.artifacts)),
  stones: mergeDropArrays(multiViewMissionData.value.map((m) => m.stones)),
  ingredients: mergeDropArrays(multiViewMissionData.value.map((m) => m.ingredients)),
  stoneFragments: mergeDropArrays(multiViewMissionData.value.map((m) => m.stoneFragments)),
  mennoData: { configs: [], totalDropsCount: 0 },
  missionCount: multiViewMissionData.value.length,
}))

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
function handleMultiKeyDown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    closeMultiMissionOverlay()
  }
}

function closeMultiMissionOverlay() {
  const el = document.querySelector('.overlay-multi-mission') as HTMLElement | null
  if (el) {
    el.style.display = 'none'
    el.classList.add('hidden')
  }
  multiMissionOverlayOpen.value = false
  multiViewMissionData.value = []
  missionsBeingViewed.value = []
  globalThis.removeEventListener('keydown', handleMultiKeyDown)
}
function openMultiMissionOverlay() {
  const el = document.querySelector('.overlay-multi-mission') as HTMLElement | null
  if (el) {
    el.style.display = 'flex'
    el.classList.remove('hidden')
  }
  multiViewDisplayMode.value = 'separate'
  multiMissionOverlayOpen.value = true
  globalThis.addEventListener('keydown', handleMultiKeyDown)
}

function handleKeyDown(event: KeyboardEvent) {
  if (!boolMissionBeingViewed.value) return
  event.preventDefault()
  event.stopPropagation()
  if (event.key === 'Escape') {
    closeMissionOverlay()
    return
  }
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
  clearFilter()
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

async function getSpecificMissionData(eid: string, missionId: string, extendedInfo: boolean, dropCache: Record<string, MissionDrop[]> | null = null): Promise<ViewMissionData | null> {
  const missionInfo = await globalThis.getMissionInfo(eid, missionId)
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

async function viewSpecificMission(missionId: string, returnValues = false, dropCache: Record<string, MissionDrop[]> | null = null): Promise<ViewMissionData | false> {
  if (isFetching.value) return false
  const newMissionViewData = await getSpecificMissionData(loadedEid.value ?? '', missionId, true, dropCache)
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
  multiViewTotalToLoad.value = missionIds.length
  const dropCache = await globalThis.getAllPlayerDrops(loadedEid.value ?? '')
  for (const missionId of missionIds) {
    const data = await viewSpecificMission(missionId, true, dropCache)
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

function properCaseTarget(target: string): string {
  return target
    .toLowerCase()
    .replaceAll('_', ' ')
    .split(' ')
    .map((w, i) => (i > 0 && (w === 'of' || w === 'the') ? w : w.charAt(0).toUpperCase() + w.slice(1)))
    .join(' ')
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
