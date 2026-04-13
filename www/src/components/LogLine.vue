<template>
  <template v-for="(seg, i) in segments" :key="i">
    <img
      v-if="seg.type === 'image'"
      :src="seg.src"
      style="display: inline; height: 1em; vertical-align: middle"
      alt=""
    />
    <span
      v-else-if="seg.type === 'eid-bar'"
      class="inline-block rounded-sm bg-current select-none"
      style="width: 16ch; height: 0.8em; vertical-align: -0.05em;"
    ></span>
    <span
      v-else-if="seg.type === 'text' && seg.color"
      :style="'color: ' + seg.color"
    >{{ seg.text }}</span>
    <span
      v-else-if="seg.type === 'text'"
      :class="isError ? 'text-red-700' : defaultClass"
    >{{ seg.text }}</span>
  </template>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { parseLogSegments } from '../composables/useLogRenderer'
import { maskEid } from '../composables/useSettings'

const props = defineProps<{
  text: string
  isError: boolean
  defaultClass?: string
}>()

const segments = computed(() => parseLogSegments(maskEid(props.text)))
</script>
