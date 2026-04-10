<template>
  <div
    v-if="visible"
    class="flex-1 flex flex-col w-full mx-auto px-4 space-y-3 overflow-y-scroll bg-darker"
  >
    <div class="text-sm text-gray-400 space-y-1">
      <p class="text-red-700">
        Your app is located in <span class="text-indigo-700">{{ appDirectory }}</span>. The
        app stores all its data relative to itself, which would pollute this shared folder.
        Please close this window, create a separate folder for EggLedger, move the app into
        it, and try again.
      </p>
      <p>See "Where does EggLedger store data?" in the About tab for more details.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAppState } from '../../composables/useAppState'

defineProps<{
  visible: boolean
}>()

const { appDirectory } = useAppState()
</script>
