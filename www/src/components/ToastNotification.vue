<template>
  <Transition name="toast">
    <div
      v-if="visible"
      class="fixed bottom-6 left-1/2 -translate-x-1/2 bg-gray-800 text-gray-100 px-5 py-3 rounded-lg border border-gray-600 shadow-xl cursor-pointer z-50 select-none whitespace-nowrap text-sm"
      @click="emit('dismissed')"
    >
      {{ message }}
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { watch } from 'vue'

const props = defineProps<{
  message: string
  visible: boolean
}>()

const emit = defineEmits<{
  dismissed: []
}>()

watch(
  () => props.visible,
  (val) => {
    if (val) {
      setTimeout(() => emit('dismissed'), 4000)
    }
  },
)
</script>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: opacity 0.25s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
}
</style>
