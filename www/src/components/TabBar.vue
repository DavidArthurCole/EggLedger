<template>
  <div class="h-11 flex-shrink-0 bg-dark">
    <div class="h-full flex items-end w-full mx-auto px-4 gap-x-1.5 bg-dark border-b border-gray-300">
      <div
        v-for="tab in tabs"
        :key="tab"
        class="relative -bottom-px px-3 pt-1.5 pb-1 text-sm font-medium text-gray-400 border border-darker_tab border-300 rounded-t-md whitespace-nowrap"
        :class="tab === activeTab ? 'bg-dark_tab border-b-transparent' : 'bg-darker_tab hover:bg-dark_tab_hover cursor-pointer'"
        @click="$emit('update:activeTab', tab)"
      >
        {{ tab }}
      </div>
      <div v-if="$slots.right" class="ml-auto">
        <slot name="right" />
      </div>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, PropType } from 'vue'

export default defineComponent({
  emits: ['update:activeTab'],
  props: {
    tabs: { type: Array as PropType<string[]>, required: true },
    activeTab: { type: String as PropType<string>, required: true },
  },
})
</script>
