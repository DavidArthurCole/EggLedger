<template>
  <div ref="resultsDiv">
    <div class="filter-form text-lg" v-if="filteredMissions && filteredMissions.length === 0">
      <span class="mt-0_5rem ml-1rem">No missions to display.</span>
    </div>
    <div
      v-else
      id="missionListDiv"
      class="mt-0_5rem px-2 py-1 shadow-sm block text-xs font-mono text-gray-500 bg-darkest rounded-md"
    >
      <div class="sticky top-0 z-10 bg-darkest -mx-2 px-2 pt-1 pb-1">
        <div class="flex items-baseline gap-2 flex-wrap">
          <span class="text-xl">
            Missions
            <button
              v-if="filteredMissions && filteredMissions.length !== 0"
              id="toggleResultsButton"
              class="text-xl toggle-link"
              type="button"
              @click="$emit('toggle-elements', $event)"
            >
              {{ allVisible ? 'Collapse All' : 'Expand All' }}
            </button>
          </span>
          <div v-if="multiViewMode === 'free' && multiViewFreeSelectIds.length > 0" class="flex items-center gap-2">
            <button
              type="button"
              class="px-3 py-1 rounded-md border border-green-700 text-sm font-medium text-green-400 bg-transparent hover:bg-green-950/50"
              @click="$emit('trigger-row-view')"
            >
              View Selected ({{ multiViewFreeSelectIds.length }})
            </button>
            <button
              type="button"
              class="px-3 py-1 rounded-md border border-gray-600 text-sm font-medium text-gray-400 bg-transparent hover:bg-darker"
              @click="$emit('deselect-all')"
            >
              Deselect All
            </button>
          </div>
        </div>
        <hr class="mt-0_5rem mb-0_5rem w-full" />
      </div>
      <template v-for="(yearVF, yearIndex) in groupedMissions" :key="yearIndex">
        <span class="text-lg font-bold mr-0_5rem ledger-underline">{{ groupedArrays.year[yearIndex].year }}</span>
        <button
          class="tb-c text-lg toggle-link"
          type="button"
          @click="$emit('toggle-elements', $event, groupedArrays.year[yearIndex])"
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
                @click="$emit('toggle-elements', $event, groupedArrays.year[yearIndex], groupedArrays.month[yearIndex][monthIndex])"
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
                      @click="$emit('toggle-elements', $event, groupedArrays.year[yearIndex], groupedArrays.month[yearIndex][monthIndex], groupedArrays.day[yearIndex][monthIndex][dayIndex])"
                    >
                      {{ groupedArrays.day[yearIndex][monthIndex][dayIndex].enabled ? 'Collapse' : 'Expand' }}
                    </button>
                    <div class="mt-1rem mission-grid">
                      <button
                        v-if="isDayRowVisible(yearIndex, monthIndex, dayIndex) && multiViewMode === 'row'"
                        :class="'hover:text-gray-600 rounded-md hover:ledger-underline text-xs font-bold btn btn-outline-dark bg-transparent mission-row-view' + (dayVF.length > 3 ? ' mission-row-view-longer' : '')"
                        @click="$emit('trigger-row-view', yearIndex, monthIndex, dayIndex)"
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
                              @change="(e) => { e.preventDefault(); $emit('multi-view-selection', e, mission.missionId) }"
                            />
                            <button
                              class="text-sm mr-5 font-bold btn btn-outline-dark bg-transparent"
                              type="button"
                              @click="$emit('view-mission', mission.missionId)"
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
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { DatabaseMission } from '../types/bridge'
import type { GroupedArrays, GroupedYear, GroupedMonth, GroupedDay } from '../composables/useMissionListGrouping'
import TargetDisplay from './TargetDisplay.vue'

const props = defineProps<{
  groupedMissions: DatabaseMission[][][][]
  groupedArrays: GroupedArrays
  allVisible: boolean
  viewByDate: boolean
  viewMissionTimes: boolean
  recolorDC: boolean
  recolorBC: boolean
  multiViewMode: 'off' | 'row' | 'free'
  multiViewFreeSelectIds: string[]
  filteredMissions: DatabaseMission[] | null
  missionBeingViewed: string | null
}>()

defineEmits<{
  'view-mission': [missionId: string]
  'toggle-elements': [event: Event, year?: GroupedYear, month?: GroupedMonth, day?: GroupedDay]
  'multi-view-selection': [event: Event, missionId: string]
  'trigger-row-view': [yearIndex?: number, monthIndex?: number, dayIndex?: number]
  'deselect-all': []
}>()

const resultsDiv = ref<HTMLElement | null>(null)

defineExpose({ resultsDiv })

function isDayRowVisible(yearIndex: number, monthIndex: number, dayIndex: number): boolean {
  if (props.viewByDate) return props.groupedArrays.day[yearIndex][monthIndex][dayIndex].enabled
  return props.groupedArrays.month[yearIndex][monthIndex].enabled
}

function getShipColorText(mission: DatabaseMission, altMode = false): string {
  if (props.missionBeingViewed === mission.missionId) {
    return altMode ? 'text-selectedmissiondarker' : 'text-selectedmission'
  }
  if (props.recolorBC && mission.isBuggedCap) {
    return altMode ? 'text-buggedcapdarker' : 'text-buggedcap'
  }
  if (props.recolorDC && mission.isDubCap) {
    return altMode ? 'text-dubcapdarker' : 'text-dubcap'
  }
  return altMode ? 'text-gray-500' : 'text-gray-400'
}

function ledgerDate(timestamp: number): Date {
  return new Date(timestamp * 1000)
}

function hhmmss(date: Date): string {
  return `${date.getHours().toString().padStart(2, '0')}:${date
    .getMinutes()
    .toString()
    .padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
}
</script>
