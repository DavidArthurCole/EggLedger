<template>
  <div class="relative" ref="containerRef">
    <button
      type="button"
      class="w-8 h-8 rounded border border-gray-600 hover:border-gray-400 transition-colors flex-shrink-0 focus:outline-none focus:border-blue-500"
      :style="{ backgroundColor: modelValue }"
      :aria-label="`Color picker, current color ${modelValue}`"
      @click="toggle"
    />

    <Teleport to="body">
      <div
        v-if="open"
        ref="dropdownRef"
        class="fixed z-50 bg-darker border border-gray-600 rounded-lg shadow-xl p-3 w-56"
        :style="dropdownStyle"
      >
        <div class="grid grid-cols-6 gap-1.5 mb-3">
          <button
            v-for="swatch in presetColors"
            :key="swatch"
            type="button"
            class="w-6 h-6 rounded border transition-all focus:outline-none"
            :class="swatch === modelValue
              ? 'border-white scale-110 ring-1 ring-white/40'
              : 'border-transparent hover:border-gray-400 hover:scale-105'"
            :style="{ backgroundColor: swatch }"
            :aria-label="swatch"
            @click="select(swatch)"
          />
        </div>

        <div class="flex items-center gap-2">
          <span class="text-xs text-gray-500 flex-shrink-0">Hex</span>
          <input
            v-model="hexInput"
            type="text"
            maxlength="7"
            class="flex-1 bg-dark border border-gray-700 rounded px-2 py-1 text-xs text-gray-300 font-mono focus:outline-none focus:border-blue-500"
            placeholder="#6366f1"
            @blur="commitHexInput"
            @keydown.enter="commitHexInput"
          />
          <div
            class="w-5 h-5 rounded border border-gray-600 flex-shrink-0"
            :style="{ backgroundColor: hexInputValid ? hexInput : modelValue }"
          />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'

const props = defineProps<{
  modelValue: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const open = ref(false)
const containerRef = ref<HTMLElement | null>(null)
const dropdownRef = ref<HTMLElement | null>(null)
const dropdownStyle = ref<Record<string, string>>({})
const hexInput = ref(props.modelValue)

const presetColors = [
  '#6366f1', '#8b5cf6', '#a855f7', '#d946ef',
  '#ec4899', '#f43f5e', '#ef4444', '#f97316',
  '#f59e0b', '#eab308', '#84cc16', '#22c55e',
  '#10b981', '#14b8a6', '#06b6d4', '#0ea5e9',
  '#3b82f6', '#60a5fa', '#64748b', '#94a3b8',
  '#e2e8f0', '#fbbf24', '#fb923c', '#c084fc',
]

const hexRegex = /^#[0-9a-fA-F]{6}$/

const hexInputValid = computed(() => hexRegex.test(hexInput.value))

watch(() => props.modelValue, (val) => {
  hexInput.value = val
})

function positionDropdown() {
  const el = containerRef.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  const viewportH = globalThis.innerHeight
  const dropH = 160
  const spaceBelow = viewportH - rect.bottom
  const top = spaceBelow >= dropH
    ? rect.bottom + 4
    : rect.top - dropH - 4
  dropdownStyle.value = {
    left: `${rect.left}px`,
    top: `${top}px`,
  }
}

function toggle() {
  if (open.value) {
    open.value = false
    return
  }
  positionDropdown()
  open.value = true
}

function select(color: string) {
  emit('update:modelValue', color)
  hexInput.value = color
  open.value = false
}

function commitHexInput() {
  if (hexInputValid.value) {
    emit('update:modelValue', hexInput.value)
  } else {
    hexInput.value = props.modelValue
  }
}

function onDocumentClick(e: MouseEvent) {
  if (!open.value) return
  const target = e.target as Node
  if (containerRef.value?.contains(target)) return
  if (dropdownRef.value?.contains(target)) return
  open.value = false
}

onMounted(() => {
  document.addEventListener('mousedown', onDocumentClick)
})

onBeforeUnmount(() => {
  document.removeEventListener('mousedown', onDocumentClick)
})
</script>
