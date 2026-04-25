<template>
  <div class="relative" ref="containerRef">
    <button
      type="button"
      class="w-8 h-8 rounded border border-gray-600 hover:border-gray-400 transition-colors flex-shrink-0 focus:outline-none focus:border-blue-500"
      :style="{ backgroundColor: currentHex }"
      :aria-label="`Color picker, current color ${currentHex}`"
      @click="toggle"
    />

    <Teleport to="body">
      <div
        v-if="open"
        ref="dropdownRef"
        class="fixed z-50 bg-darker border border-gray-600 rounded-lg shadow-xl p-3"
        style="width: 220px"
        :style="dropdownStyle"
      >
        <!-- Color wheel -->
        <div class="flex flex-col items-center gap-2 mb-3">
          <div
            class="relative rounded-full cursor-crosshair flex-shrink-0"
            style="width: 140px; height: 140px"
            ref="wheelRef"
            @mousedown.prevent="onWheelMousedown"
          >
            <div
              class="absolute inset-0 rounded-full"
              style="background: conic-gradient(red, yellow, lime, aqua, blue, magenta, red)"
            />
            <div
              class="absolute inset-0 rounded-full"
              style="background: radial-gradient(circle, white 0%, transparent 70%)"
            />
            <div
              class="absolute inset-0 rounded-full"
              style="background: radial-gradient(circle, transparent 60%, rgba(0,0,0,0.55) 100%)"
            />
            <div
              class="absolute w-3 h-3 rounded-full border-2 border-white shadow pointer-events-none -translate-x-1/2 -translate-y-1/2"
              :style="dotStyle"
            />
          </div>

          <div class="w-full flex flex-col gap-1.5">
            <label class="flex items-center gap-2 text-xs text-gray-500">
              <span class="w-4">S</span>
              <input
                type="range"
                min="0"
                max="100"
                :value="hsl.s"
                class="flex-1 h-1.5 accent-indigo-500"
                @input="onSaturationInput"
              />
              <span class="w-7 text-right tabular-nums text-gray-400">{{ hsl.s }}%</span>
            </label>
            <label class="flex items-center gap-2 text-xs text-gray-500">
              <span class="w-4">L</span>
              <input
                type="range"
                min="10"
                max="90"
                :value="hsl.l"
                class="flex-1 h-1.5 accent-indigo-500"
                @input="onLightnessInput"
              />
              <span class="w-7 text-right tabular-nums text-gray-400">{{ hsl.l }}%</span>
            </label>
          </div>
        </div>

        <!-- Preset swatches -->
        <div class="grid grid-cols-6 gap-1 mb-3">
          <button
            v-for="swatch in presetColors"
            :key="swatch"
            type="button"
            class="rounded border transition-all focus:outline-none"
            style="width: 22px; height: 22px"
            :class="swatch === currentHex
              ? 'border-white scale-110 ring-1 ring-white/40'
              : 'border-transparent hover:border-gray-400 hover:scale-105'"
            :style="{ backgroundColor: swatch }"
            :aria-label="swatch"
            @click="select(swatch)"
          />
        </div>

        <!-- Hex input -->
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
            class="rounded border border-gray-600 flex-shrink-0"
            style="width: 20px; height: 20px"
            :style="{ backgroundColor: hexInputValid ? hexInput : currentHex }"
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

const presetColors = [
  '#f43f5e', '#ef4444', '#f97316', '#f59e0b',
  '#22c55e', '#10b981', '#14b8a6', '#06b6d4',
  '#3b82f6', '#6366f1', '#8b5cf6', '#a855f7',
  '#d946ef', '#ec4899', '#64748b', '#94a3b8',
  '#e2e8f0', '#fbbf24', '#fb923c', '#c084fc',
  '#60a5fa', '#34d399', '#f9a8d4', '#ffffff',
]

const hexRegex = /^#[0-9a-fA-F]{6}$/

// HSL state - source of truth for the wheel+sliders
const hsl = ref({ h: 220, s: 65, l: 55 })

const open = ref(false)
const containerRef = ref<HTMLElement | null>(null)
const dropdownRef = ref<HTMLElement | null>(null)
const wheelRef = ref<HTMLElement | null>(null)
const dropdownStyle = ref<Record<string, string>>({})
const hexInput = ref('')

/** Convert HSL integers to #rrggbb */
function hslToHex(h: number, s: number, l: number): string {
  const sl = s / 100
  const ll = l / 100
  const a = sl * Math.min(ll, 1 - ll)
  const f = (n: number) => {
    const k = (n + h / 30) % 12
    const color = ll - a * Math.max(Math.min(k - 3, 9 - k, 1), -1)
    return Math.round(255 * color).toString(16).padStart(2, '0')
  }
  return `#${f(0)}${f(8)}${f(4)}`
}

/** Parse any color string to #rrggbb. Handles hex and hsl(...) strings. */
function normalizeToHex(value: string): string {
  if (hexRegex.test(value)) return value.toLowerCase()
  // Handle hsl(...) strings
  const hslMatch = value.match(/hsl\(\s*([\d.]+)\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*\)/)
  if (hslMatch) {
    return hslToHex(
      Number.parseFloat(hslMatch[1]),
      Number.parseFloat(hslMatch[2]),
      Number.parseFloat(hslMatch[3]),
    )
  }
  return '#6366f1'
}

/** Parse #rrggbb to { h, s, l } with integer percentages */
function hexToHsl(hex: string): { h: number; s: number; l: number } {
  const r = Number.parseInt(hex.slice(1, 3), 16) / 255
  const g = Number.parseInt(hex.slice(3, 5), 16) / 255
  const b = Number.parseInt(hex.slice(5, 7), 16) / 255
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  const ll = (max + min) / 2
  if (max === min) return { h: 0, s: 0, l: Math.round(ll * 100) }
  const d = max - min
  const ss = ll > 0.5 ? d / (2 - max - min) : d / (max + min)
  let hh = 0
  if (max === r) hh = ((g - b) / d + (g < b ? 6 : 0)) / 6
  else if (max === g) hh = ((b - r) / d + 2) / 6
  else hh = ((r - g) / d + 4) / 6
  return {
    h: Math.round(hh * 360),
    s: Math.round(ss * 100),
    l: Math.round(ll * 100),
  }
}

const currentHex = computed(() => normalizeToHex(props.modelValue))

const hexInputValid = computed(() => hexRegex.test(hexInput.value))

/** Position of the dot on the wheel based on hue and saturation */
const dotStyle = computed(() => {
  const rad = ((hsl.value.h - 90) * Math.PI) / 180
  // saturation maps to radius: 0% at center, 100% at edge (scaled to ~45%)
  const r = (hsl.value.s / 100) * 45
  const x = 50 + r * Math.cos(rad)
  const y = 50 + r * Math.sin(rad)
  return {
    left: `${x}%`,
    top: `${y}%`,
    backgroundColor: currentHex.value,
  }
})

function syncFromHex(hex: string) {
  const parsed = hexToHsl(hex)
  hsl.value = parsed
  hexInput.value = hex
}

watch(currentHex, (val) => {
  syncFromHex(val)
}, { immediate: true })

function emitCurrentHsl() {
  const hex = hslToHex(hsl.value.h, hsl.value.s, hsl.value.l)
  hexInput.value = hex
  emit('update:modelValue', hex)
}

function onWheelMousedown(e: MouseEvent) {
  pickWheelColor(e)
  const onMove = (me: MouseEvent) => pickWheelColor(me)
  const onUp = () => {
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
}

function pickWheelColor(e: MouseEvent) {
  const el = wheelRef.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  const cx = rect.left + rect.width / 2
  const cy = rect.top + rect.height / 2
  const dx = e.clientX - cx
  const dy = e.clientY - cy
  const radius = rect.width / 2
  const dist = Math.hypot(dx, dy)
  const rawAngle = Math.atan2(dy, dx) * (180 / Math.PI) + 90
  const hue = ((rawAngle % 360) + 360) % 360
  const sat = Math.round(Math.min(dist / radius, 1) * 100)
  hsl.value = { h: Math.round(hue), s: sat, l: hsl.value.l }
  emitCurrentHsl()
}

function onSaturationInput(e: Event) {
  hsl.value.s = Number.parseInt((e.target as HTMLInputElement).value, 10)
  emitCurrentHsl()
}

function onLightnessInput(e: Event) {
  hsl.value.l = Number.parseInt((e.target as HTMLInputElement).value, 10)
  emitCurrentHsl()
}

function positionDropdown() {
  const el = containerRef.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  const viewportH = globalThis.innerHeight
  const dropH = 380
  const spaceBelow = viewportH - rect.bottom
  const top = spaceBelow >= dropH ? rect.bottom + 4 : rect.top - dropH - 4
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
  syncFromHex(color)
  emit('update:modelValue', color)
  open.value = false
}

function commitHexInput() {
  if (hexInputValid.value) {
    const lower = hexInput.value.toLowerCase()
    syncFromHex(lower)
    emit('update:modelValue', lower)
  } else {
    hexInput.value = currentHex.value
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
