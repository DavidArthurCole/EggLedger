<template>
  <!-- Single mission overlay -->
  <div
    v-if="mode === 'single'"
    v-show="open"
    class="mission-view-overlay overlay-mission"
    @click.self="$emit('close')"
  >
    <div class="w-60vw max-h-95vh bg-dark rounded-lg relative flex flex-col" @click.stop>
      <!-- Close button -->
      <button
        class="absolute top-2 right-2 z-10 rounded-md text-gray-400 hover:text-gray-200 focus:outline-none"
        type="button"
        @click="$emit('close')"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <!-- Left arrow -->
      <button
        :disabled="!missionData?.prevMission"
        @click="missionData?.prevMission && $emit('view', missionData.prevMission)"
        title="Previous mission"
        class="disabled:cursor-not-allowed absolute left-2 top-1/2 -translate-y-1/2 z-10 rounded-md text-gray-400 focus:outline-none disabled:text-gray-600 hover:text-gray-200"
      >
        <svg class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </button>
      <!-- Right arrow -->
      <button
        :disabled="!missionData?.nextMission"
        @click="missionData?.nextMission && $emit('view', missionData.nextMission)"
        title="Next mission"
        class="disabled:cursor-not-allowed absolute right-2 top-1/2 -translate-y-1/2 z-10 rounded-md text-gray-400 focus:outline-none disabled:text-gray-600 hover:text-gray-200"
      >
        <svg class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
        </svg>
      </button>
      <!-- Scrollable content -->
      <div class="overflow-auto flex-1 min-h-0 pt-8 px-10 pb-2">
        <div class="bg-darkerer rounded-lg py-2">
          <ShipDisplay
            v-if="missionData && missionData.missionInfo != null"
            :view-mission-data="missionData"
            :is-multi="false"
            :hide-footer-tip="true"
            :show-expected-drops="showExpectedDrops"
            @view="(val: string) => $emit('view', val)"
          />
        </div>
      </div>
      <!-- Tip - pinned below scroll area in the bg-dark zone -->
      <div class="text-xs text-gray-300 text-center flex-shrink-0 pb-2">
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

  <!-- Multi mission overlay -->
  <div
    v-else
    v-show="open"
    class="mission-view-overlay overlay-multi-mission"
    @click.self="$emit('close')"
  >
    <div :class="(isLoading ? 'min-w-40vw max-w-40vw' : 'min-w-60vw max-w-85vw') + ' max-h-95vh bg-dark rounded-lg relative p-1rem flex flex-col'" @click.stop>
      <button class="close-button" type="button" @click="$emit('close')">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>

      <!-- Loading state -->
      <div v-if="isLoading" class="text-center px-8 py-4 min-w-60">
        <div class="mt-0_5rem text-center text-xl text-gray-400">Multi-Mission View Loading</div>
        <hr class="mt-1rem mb-1rem w-full" />
        <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
        <div v-if="dropCachePreloading" class="mt-3 text-sm text-gray-400">
          Pre-loading drop cache...
        </div>
        <template v-else>
          <div class="mt-3 text-sm text-gray-400">
            Loading mission
            <span class="text-gray-200 font-semibold">{{ (loadedCount ?? 0) + 1 }}</span>
            of
            <span class="text-gray-200 font-semibold">{{ totalToLoad }}</span>
          </div>
          <div class="mt-2 w-full bg-darker rounded-full h-1.5 overflow-hidden">
            <div
              class="bg-blue-500 h-1.5 rounded-full transition-all duration-300"
              :style="{ width: (totalToLoad ?? 0) > 0 ? Math.min(100, (((loadedCount ?? 0) + 1) / (totalToLoad ?? 1)) * 100) + '%' : '0%' }"
            />
          </div>
        </template>
      </div>

      <!-- Loaded content -->
      <div v-else class="flex flex-col flex-1 min-h-0">
        <!-- Toggle - pinned above scroll area -->
        <div class="flex justify-center mb-3 flex-shrink-0">
          <div class="flex bg-darkerer border border-gray-700 rounded-md p-0.5 text-sm gap-0.5">
            <button
              type="button"
              :disabled="(multiMissionData?.length ?? 0) > 20"
              :class="displayMode === 'separate' ? 'bg-dark text-blue-400' : (multiMissionData?.length ?? 0) > 20 ? 'text-gray-600 cursor-not-allowed' : 'text-gray-500 hover:text-gray-300'"
              class="px-3 py-1 rounded focus:outline-none transition-colors"
              @click="displayMode = 'separate'"
            >Separate</button>
            <button
              type="button"
              :class="displayMode === 'combined' ? 'bg-dark text-blue-400' : 'text-gray-500 hover:text-gray-300'"
              class="px-3 py-1 rounded focus:outline-none transition-colors"
              @click="displayMode = 'combined'"
            >Combined</button>
          </div>
        </div>

        <!-- Separate mode - scrollable, fills remaining height -->
        <div v-if="displayMode === 'separate'" class="flex justify-between gap-2 flex-1 min-h-0 overflow-auto mb-3">
          <div
            v-for="(missionData, i) in multiMissionData"
            :key="i"
            :class="'h-full w-1/' + multiMissionData!.length"
            class="bg-darkerer rounded-lg py-2 flex-shrink-0"
          >
            <ShipDisplay
              v-if="missionData && missionData.missionInfo != null"
              :is-first="i === 0"
              :is-last="i === multiMissionData!.length - 1"
              :view-mission-data="missionData"
              :is-multi="true"
              :show-expected-drops="showExpectedDrops"
              :ship-count="multiMissionData!.length"
            />
          </div>
        </div>

        <!-- Combined mode - scrollable, fills remaining height -->
        <div v-else class="bg-darkerer rounded-lg p-3 text-gray-300 text-center flex-1 min-h-0 overflow-auto mb-3">
          <div class="flex flex-wrap justify-center gap-x-3 gap-y-2 mb-4">
            <div
              v-for="(missionData, i) in (shipHeaderExpanded ? multiMissionData : multiMissionData?.slice(0, 20))"
              :key="i"
              class="flex flex-col items-center rounded-lg px-3 py-2 min-w-36"
              style="background: rgba(120, 128, 138, 0.12)"
            >
              <img
                v-if="missionData.missionInfo.shipEnumString"
                :src="'images/ships/' + missionData.missionInfo.shipEnumString + '.png'"
                :alt="missionData.missionInfo.shipString"
                class="w-14 h-14 object-contain mb-1"
              />
              <div class="font-semibold text-sm" :class="'text-duration-' + missionData.missionInfo.durationType">
                {{ missionData.missionInfo.shipString }}
              </div>
              <span v-if="missionData.missionInfo.level > 0" class="text-goldenstar text-xs">
                ({{ missionData.missionInfo.level }}<span>&#9733;</span>)
              </span>
              <div class="text-gray-400 text-xs mt-1">{{ formatShortDate(missionData.launchDT) }}</div>
              <div class="text-gray-400 text-xs">{{ formatShortDate(missionData.returnDT) }}</div>
              <div
                v-if="missionData.missionInfo.target && missionData.missionInfo.target.toUpperCase() !== 'UNKNOWN'"
                class="text-gray-400 text-xs mt-1"
              >
                {{ properCaseTarget(missionData.missionInfo.target) }}
              </div>
              <div class="text-gray-500 text-xs mt-0.5 flex items-center gap-1">
                <span>{{ missionData.missionInfo.capacity }}</span>
                <span v-if="missionData.missionInfo.isBuggedCap" class="text-buggedcap">0.6x</span>
                <span v-else-if="missionData.missionInfo.isDubCap" class="text-dubcap">{{ missionData.capacityModifier }}x</span>
              </div>
            </div>
            <button
              v-if="!shipHeaderExpanded && (multiMissionData?.length ?? 0) > 20"
              type="button"
              class="flex flex-col items-center justify-center rounded-lg px-3 py-2 min-w-36 text-gray-400 hover:text-gray-200 transition-colors"
              style="background: rgba(120, 128, 138, 0.12)"
              @click="shipHeaderExpanded = true"
            >
              + {{ (multiMissionData?.length ?? 0) - 20 }} more
            </button>
          </div>
          <drop-display-container
            ledger-type="mission"
            :data="combinedDropData"
            :show-expected-drops="false"
            :use-containers="true"
          />
        </div>

        <!-- Hover text - pinned below scroll area -->
        <div class="text-xs text-gray-300 text-center flex-shrink-0">
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
</template>

<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue'
import type { ViewMissionData, InnerDrop } from '../types/missionView'
import ShipDisplay from './ShipDisplay.vue'
import DropDisplayContainer from './DropDisplayContainer.vue'
import { type DropLike, sortGroupAlreadyCombed, inventoryVisualizerSort } from '../composables/useMissionSorting'

const props = defineProps<{
  mode: 'single' | 'multi'
  open: boolean
  missionData?: ViewMissionData | null
  multiMissionData?: ViewMissionData[]
  isLoading?: boolean
  dropCachePreloading?: boolean
  totalToLoad?: number
  loadedCount?: number
  showExpectedDrops?: boolean
  sortMethod?: 'default' | 'iv'
}>()

const emit = defineEmits<{
  'close': []
  'view': [missionId: string]
}>()

const displayMode = ref<'separate' | 'combined'>('separate')
const shipHeaderExpanded = ref(false)

watch(() => props.open, (val) => {
  if (val && props.mode === 'multi') {
    displayMode.value = (props.totalToLoad ?? 0) > 5 ? 'combined' : 'separate'
    shipHeaderExpanded.value = false
  }
})

// Merge arrays of drops by id+level+rarity key, summing counts
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

const combinedDropData = computed(() => {
  const missions = props.multiMissionData ?? []
  const sortFn = props.sortMethod === 'iv' ? inventoryVisualizerSort : sortGroupAlreadyCombed
  const sort = (arr: InnerDrop[]) => sortFn(arr as unknown as DropLike[]) as unknown as InnerDrop[]
  return {
    artifacts: sort(mergeDropArrays(missions.map((m) => m.artifacts))),
    stones: sort(mergeDropArrays(missions.map((m) => m.stones))),
    ingredients: sort(mergeDropArrays(missions.map((m) => m.ingredients))),
    stoneFragments: sort(mergeDropArrays(missions.map((m) => m.stoneFragments))),
    mennoData: { configs: [], totalDropsCount: 0 },
    missionCount: missions.length,
  }
})

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

function openUrl(url: string) {
  globalThis.openURL(url)
}

// Keyboard handling

function handleSingleKeyDown(event: KeyboardEvent) {
  event.preventDefault()
  event.stopPropagation()
  if (event.key === 'Escape') {
    emit('close')
    return
  }
  const data = props.missionData
  if (!data) return
  if (event.key === 'ArrowLeft' && data.prevMission) {
    emit('view', data.prevMission)
  } else if (event.key === 'ArrowRight' && data.nextMission) {
    emit('view', data.nextMission)
  }
}

function handleMultiKeyDown(event: KeyboardEvent) {
  if (event.key === 'Escape') emit('close')
}

watch(() => props.open, (val) => {
  if (props.mode === 'single') {
    if (val) globalThis.addEventListener('keydown', handleSingleKeyDown)
    else globalThis.removeEventListener('keydown', handleSingleKeyDown)
  } else if (val) globalThis.addEventListener('keydown', handleMultiKeyDown)
  else globalThis.removeEventListener('keydown', handleMultiKeyDown)
})

onUnmounted(() => {
  globalThis.removeEventListener('keydown', handleSingleKeyDown)
  globalThis.removeEventListener('keydown', handleMultiKeyDown)
})
</script>
