<template>
  <Transition name="progress-fade">
    <div v-if="panelVisible" class="px-2 py-1 text-sm text-gray-400 bg-darkest rounded-md tabular-nums">
      <div v-if="statusText || isSpinning" class="mb-1">
        <img v-if="isSpinning" :src="'images/loading.gif'" alt="Loading..." class="target-ico inline-block" />
        <span v-if="statusText" :class="statusClass">{{ statusText }}</span>
      </div>
      <div class="flex gap-1">
        <div
          v-for="seg in segments"
          :key="seg.label"
          class="flex-1 h-3 bg-dark rounded-sm overflow-hidden"
        >
          <div
            class="h-full transition-all duration-300 rounded-sm"
            :class="seg.pulsing ? 'animate-pulse' : ''"
            :style="segmentBarStyle(seg)"
          ></div>
        </div>
      </div>
      <div class="flex mt-0.5 text-gray-600" style="font-size: 0.6rem">
        <div
          v-for="seg in segments"
          :key="seg.label"
          class="flex-1 text-center"
          :class="seg.pulsing ? 'animate-pulse' : ''"
          :style="seg.status !== 'pending' ? { color: resolveColor(seg.color) } : undefined"
        >{{ seg.label }}</div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue'

export interface ProgressSegment {
  label: string
  status: 'pending' | 'active' | 'done' | 'failed' | 'skipped'
  /** Named color: 'blue' | 'violet' | 'green', or a raw CSS color string. */
  color: string
  /** Percentage fill (3-100) when status is 'active'. Defaults to 100. */
  widthPct?: number
  /** Pulse the bar and label when true. */
  pulsing?: boolean
}

const props = defineProps<{
  /** True while the operation is running. Panel shows on true, fades 7s after false. */
  active: boolean
  segments: ProgressSegment[]
  /** Optional status line above the bar. */
  statusText?: string
  /** CSS class applied to the status text (e.g. Tailwind color class). */
  statusClass?: string
  /** Show the loading spinner next to status text. */
  isSpinning?: boolean
}>()

const colorMap: Record<string, string> = {
  blue: 'rgb(59 130 246)',
  violet: 'rgb(139 92 246)',
  green: 'rgb(16 185 129)',
  red: 'rgb(220 38 38)',
}

function resolveColor(color: string): string {
  return colorMap[color] ?? color
}

function resolveColorRgba(color: string, alpha: number): string {
  const base = resolveColor(color)
  // base is "rgb(r g b)" - use CSS Color Level 4 slash syntax: "rgb(r g b / alpha)"
  return base.replace(')', ` / ${alpha})`)
}

function segmentBarStyle(seg: ProgressSegment): Record<string, string> {
  if (seg.status === 'pending') return { width: '0%' }

  if (seg.status === 'skipped') {
    const c = resolveColorRgba(seg.color, 0.3)
    return {
      width: '100%',
      backgroundImage: `repeating-linear-gradient(45deg, transparent, transparent 3px, ${c} 3px, ${c} 5px)`,
    }
  }

  if (seg.status === 'failed') {
    return { width: '100%', backgroundColor: colorMap.red }
  }

  // active or done
  const width = seg.status === 'active' ? `${seg.widthPct ?? 100}%` : '100%'
  return { width, backgroundColor: resolveColor(seg.color) }
}

const panelVisible = ref(false)
let fadeTimer: ReturnType<typeof setTimeout> | null = null

watch(() => props.active, (isActive) => {
  if (isActive) {
    if (fadeTimer !== null) { clearTimeout(fadeTimer); fadeTimer = null }
    panelVisible.value = true
  } else if (panelVisible.value) {
    fadeTimer = setTimeout(() => {
      panelVisible.value = false
      fadeTimer = null
    }, 7000)
  }
})

onUnmounted(() => {
  if (fadeTimer !== null) clearTimeout(fadeTimer)
})
</script>

<style scoped>
.progress-fade-enter-active {
  transition: opacity 0.3s ease;
}
.progress-fade-leave-active {
  transition: opacity 0.6s ease;
}
.progress-fade-enter-from,
.progress-fade-leave-to {
  opacity: 0;
}
</style>
