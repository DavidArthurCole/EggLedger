<template>
  <div class="flex flex-row gap-4 items-start">
    <!-- Grid size picker -->
    <div class="flex flex-col gap-2">
      <span class="text-xs text-gray-400 flex items-center gap-1">
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 5a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1H5a1 1 0 01-1-1V5zM14 5a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1V5zM4 15a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1H5a1 1 0 01-1-1v-4zM14 15a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1v-4z"/></svg>
        Grid size
        <span class="text-gray-500 ml-1">{{ gridW }}x{{ gridH }}</span>
      </span>
      <!-- 8x4 visual picker -->
      <div class="flex flex-col gap-0.5 self-start" @mouseleave="clearHover()">
        <div v-for="r in 4" :key="r" class="flex gap-0.5">
          <div
            v-for="c in 8"
            :key="c"
            class="w-3.5 h-3.5 rounded-sm border cursor-pointer transition-colors"
            :class="cellClass(c, r)"
            @mouseenter="setHover(c, r)"
            @click="selectCell(c, r)"
          />
        </div>
      </div>
    </div>

    <!-- Live preview -->
    <div class="flex flex-col gap-1">
      <span class="text-xs text-gray-400">Preview</span>
      <div
        class="bg-darker rounded-lg border border-gray-700 p-3 overflow-hidden flex flex-col"
        :style="{ width: (gridW * 110) + 'px', height: (gridH * 110) + 'px' }"
      >
        <div class="text-xs font-medium text-gray-300 mb-2 truncate flex-shrink-0">{{ name || 'Untitled report' }}</div>
        <div class="flex-1 min-h-0">
          <template v-if="displayMode === 'bar'">
            <div class="flex flex-col justify-center gap-1 h-full">
              <div v-for="w in [80, 55, 40, 25]" :key="w" class="flex items-center gap-1">
                <div class="w-8 h-1.5 bg-gray-700 rounded-sm flex-shrink-0" />
                <div class="h-1.5 rounded-sm" :style="{ width: w + '%', backgroundColor: color + '88' }" />
              </div>
            </div>
          </template>
          <template v-else-if="displayMode === 'line'">
            <div class="flex items-end gap-0.5 h-full">
              <div v-for="(h, i) in [30, 55, 45, 70, 50, 80, 60]" :key="i" class="flex-1 rounded-sm" :style="{ height: h + '%', backgroundColor: color + '88' }" />
            </div>
          </template>
          <template v-else-if="displayMode === 'pie'">
            <svg viewBox="0 0 100 100" class="w-full h-full">
              <path d="M 50 50 L 50 10 A 40 40 0 0 1 73.5 82.4 Z" :fill="color + 'ee'" />
              <path d="M 50 50 L 73.5 82.4 A 40 40 0 0 1 10 50 Z" :fill="color + '99'" />
              <path d="M 50 50 L 10 50 A 40 40 0 0 1 50 10 Z" :fill="color + '55'" />
            </svg>
          </template>
          <template v-else>
            <div class="flex flex-col justify-center gap-0.5 h-full">
              <div v-for="i in 4" :key="i" class="flex gap-2 py-0.5 border-b border-gray-700/50 last:border-0">
                <div class="flex-1 h-1.5 bg-gray-700 rounded-sm" />
                <div class="w-8 h-1.5 bg-gray-600 rounded-sm" />
              </div>
            </div>
          </template>
        </div>
      </div>
      <p class="text-xs text-gray-500 text-center">{{ gridW }}x{{ gridH }} - {{ modeLabel }} - {{ subjectLabel }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  gridW: number
  gridH: number
  displayMode: string
  color: string
  name: string
  modeLabel: string
  subjectLabel: string
}>()

const emit = defineEmits<{
  'update:gridW': [value: number]
  'update:gridH': [value: number]
}>()

const hoverW = ref(0)
const hoverH = ref(0)

function cellClass(c: number, r: number): string {
  const confirmed = c <= props.gridW && r <= props.gridH
  if (hoverW.value > 0) {
    const hovered = c <= hoverW.value && r <= hoverH.value
    if (confirmed && hovered) return 'bg-indigo-600 border-indigo-500'
    if (hovered) return 'bg-indigo-600/75 border-indigo-500/50'
    if (confirmed) return 'bg-indigo-800/60 border-indigo-700/50'
    return 'bg-gray-800 border-gray-600 hover:border-gray-400'
  }
  return confirmed ? 'bg-indigo-600 border-indigo-500' : 'bg-gray-800 border-gray-600 hover:border-gray-400'
}

function clearHover() {
  hoverW.value = 0
  hoverH.value = 0
}

function setHover(c: number, r: number) {
  hoverW.value = c
  hoverH.value = r
}

function selectCell(c: number, r: number) {
  emit('update:gridW', c)
  emit('update:gridH', r)
}
</script>
