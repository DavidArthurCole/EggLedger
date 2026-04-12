<template>
  <div v-if="processes.length > 0" class="flex flex-col gap-1 overflow-y-auto max-h-48 flex-shrink-0">
    <div
      v-for="proc in processes"
      :key="proc.id"
      class="bg-darkest rounded-md overflow-hidden text-xs font-mono"
    >
      <button
        class="w-full flex items-center gap-2 px-2 py-1 text-gray-400 hover:text-gray-300 text-left"
        type="button"
        @click="toggle(proc.id)"
      >
        <span
          :class="[
            'w-2 h-2 rounded-full flex-shrink-0',
            proc.status === 'running' ? 'bg-yellow-400 animate-pulse' :
            proc.status === 'done' ? 'bg-green-500' :
            'bg-red-500',
          ]"
        ></span>
        <span class="flex-1 truncate">{{ proc.label }}</span>
        <div class="flex gap-1 flex-shrink-0">
          <span
            v-for="seg in proc.segments"
            :key="seg.name"
            :title="seg.name"
            :class="[
              'w-2 h-2 rounded-full',
              seg.status === 'active' ? 'bg-blue-400 animate-pulse' :
              seg.status === 'done' ? 'bg-green-500' :
              seg.status === 'failed' ? 'bg-red-500' :
              'bg-gray-600',
            ]"
          ></span>
        </div>
        <span class="text-gray-600 flex-shrink-0 ml-1">{{ relativeTime(proc.startTimestamp) }}</span>
        <span class="text-gray-600 flex-shrink-0 ml-1">{{ isExpanded(proc.id) ? '▼' : '▶' }}</span>
      </button>

      <div
        v-show="isExpanded(proc.id)"
        class="px-2 pb-1 max-h-32 overflow-y-auto border-t border-dark"
      >
        <div v-if="proc.logs.length === 0" class="text-gray-600 italic py-0.5">No logs yet.</div>
        <div
          v-for="(entry, i) in proc.logs"
          :key="i"
          class="whitespace-pre"
        >
          <template v-for="(seg, j) in parseLogSegments(maskEid(entry.text))" :key="j">
            <img
              v-if="seg.type === 'image'"
              :src="seg.src"
              style="display: inline; height: 1em; vertical-align: middle"
              alt=""
            />
            <span
              v-else-if="seg.type === 'text' && seg.color"
              :style="'color: ' + seg.color"
            >{{ seg.text }}</span>
            <span
              v-else
              :class="entry.isError ? 'text-red-700' : 'text-gray-400'"
            >{{ seg.text }}</span>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from 'vue'
import type { ProcessSnapshot } from '../types/bridge'
import { parseLogSegments } from '../composables/useLogRenderer'
import { maskEid } from '../composables/useSettings'

const props = defineProps<{
  processes: ProcessSnapshot[]
}>()

const expandedIds = ref(new Set<string>())
const now = ref(Date.now())
let timer: ReturnType<typeof setInterval>

onMounted(() => {
  timer = setInterval(() => { now.value = Date.now() }, 1000)
})
onUnmounted(() => {
  clearInterval(timer)
})

function isExpanded(id: string): boolean {
  return expandedIds.value.has(id)
}

function toggle(id: string) {
  const next = new Set(expandedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  expandedIds.value = next
}

// Auto-expand all currently running processes.
watch(
  () => props.processes,
  (procs) => {
    const running = procs.filter((p) => p.status === 'running')
    if (running.length === 0) return
    const next = new Set(expandedIds.value)
    for (const p of running) next.add(p.id)
    expandedIds.value = next
  },
  { deep: true, immediate: true },
)

function relativeTime(ms: number): string {
  const seconds = Math.floor((now.value - ms) / 1000)
  if (seconds < 60) return `${seconds}s ago`
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  return `${Math.floor(minutes / 60)}h ago`
}
</script>
