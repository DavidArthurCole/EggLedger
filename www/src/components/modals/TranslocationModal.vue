<template>
  <div
    v-if="visible"
    class="flex-1 flex flex-col w-full mx-auto px-4 space-y-3 overflow-y-scroll bg-darker"
  >
    <div class="text-sm text-gray-400 space-y-1">
      <p class="text-red-700">
        EggLedger.app was launched in a jailed environment due to a macOS security feature.
        The app cannot write data to disk in this environment. To work around this, run the
        <span class="text-indigo-700">preflight</span> script which came with the app, then
        restart the app. See <span class="text-indigo-700">README.html</span> (also came
        with the app) for details.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  visible: boolean
}>()
</script>
