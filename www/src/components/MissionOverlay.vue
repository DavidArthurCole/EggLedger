<template>
  <!-- Single mission overlay -->
  <div
    v-if="mode === 'single'"
    v-show="open"
    class="mission-view-overlay overlay-mission"
    @click.self="$emit('close')"
  >
    <div class="max-w-70vw max-h-90vh overflow-auto bg-dark rounded-lg relative p-1rem" @click.stop>
      <button class="close-button" type="button" @click="$emit('close')">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <div class="bg-darkerer rounded-lg py-2">
        <ShipDisplay
          v-if="missionData && missionData.missionInfo != null"
          :view-mission-data="missionData"
          :is-multi="false"
          :show-expected-drops="showExpectedDrops"
          @view="(val: string) => $emit('view', val)"
        />
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
    <div class="max-w-90vw max-h-90vh bg-dark rounded-lg relative p-1rem" @click.stop>
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
              :style="{ width: (totalToLoad ?? 0) > 0 ? (((loadedCount ?? 0) / (totalToLoad ?? 1)) * 100) + '%' : '0%' }"
            />
          </div>
        </template>
      </div>

      <!-- Loaded content -->
      <div v-else class="max-w-90vw max-h-80vh overflow-auto">
        <div class="flex justify-center mb-3">
          <div class="flex bg-darkerer border border-gray-700 rounded-md p-0.5 text-sm gap-0.5">
            <button
              type="button"
              :class="displayMode === 'separate' ? 'bg-dark text-blue-400' : 'text-gray-500 hover:text-gray-300'"
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

        <!-- Separate mode -->
        <div v-if="displayMode === 'separate'" class="flex justify-between gap-2 mb-4">
          <div
            v-for="(missionData, i) in multiMissionData"
            :key="i"
            :class="'w-1/' + multiMissionData!.length"
            class="bg-darkerer rounded-lg py-2"
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

        <!-- Combined mode -->
        <div v-else class="bg-darkerer rounded-lg p-3 text-gray-300 text-center">
          <div class="flex flex-wrap justify-center gap-x-3 gap-y-2 mb-4">
            <div
              v-for="(missionData, i) in multiMissionData"
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
          </div>
          <drop-display-container
            ledger-type="mission"
            :data="combinedDropData"
            :show-expected-drops="false"
            :use-containers="true"
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
</template>

<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue'
import type { ViewMissionData, InnerDrop } from '../types/missionView'
import ShipDisplay from './ShipDisplay.vue'
import DropDisplayContainer from './DropDisplayContainer.vue'

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
}>()

const emit = defineEmits<{
  'close': []
  'view': [missionId: string]
}>()

const displayMode = ref<'separate' | 'combined'>('separate')

watch(() => props.open, (val) => {
  if (val && props.mode === 'multi') displayMode.value = 'separate'
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
  return {
    artifacts: mergeDropArrays(missions.map((m) => m.artifacts)),
    stones: mergeDropArrays(missions.map((m) => m.stones)),
    ingredients: mergeDropArrays(missions.map((m) => m.ingredients)),
    stoneFragments: mergeDropArrays(missions.map((m) => m.stoneFragments)),
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
  } else {
    if (val) globalThis.addEventListener('keydown', handleMultiKeyDown)
    else globalThis.removeEventListener('keydown', handleMultiKeyDown)
  }
})

onUnmounted(() => {
  globalThis.removeEventListener('keydown', handleSingleKeyDown)
  globalThis.removeEventListener('keydown', handleMultiKeyDown)
})
</script>
