<template>
  <dialog
    ref="dialogRef"
    class="updating-overlay bg-transparent w-full h-full max-w-full max-h-full m-0 p-0 z-50 fixed top-0 left-0 right-0 bottom-0"
  >
    <div class="popup-selector-main w-full h-full flex items-center justify-center">
      <div class="max-w-50vw bg-dark rounded-lg relative p-2rem">
        <div class="text-gray-300 space-y-2 flex-auto flex-col text-center flex items-center justify-center">
          <template v-if="overlayMode === 'updating'">
            <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
            <span class="text-lg text-gray-400 mt-1rem">Updating, please wait...</span>
          </template>
          <template v-else-if="overlayMode === 'countdown'">
            <span class="text-lg text-gray-300">Update ready.</span>
            <span class="text-sm text-gray-400 tabular-nums mt-0_5rem">Closing in {{ countdownValue }}s...</span>
          </template>
        </div>
      </div>
    </div>
  </dialog>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue'
import { useUpdateOverlay } from '../../composables/useUpdateOverlay'

const { overlayMode, countdownValue, end } = useUpdateOverlay()

const dialogRef = ref<HTMLDialogElement | null>(null)

watch(overlayMode, (mode) => {
  const dialog = dialogRef.value
  if (!dialog) return
  if (mode === 'idle') {
    if (dialog.open) dialog.close()
  } else if (!dialog.open) {
    dialog.showModal()
  }
}, { immediate: true })

onUnmounted(() => {
  end()
})
</script>
