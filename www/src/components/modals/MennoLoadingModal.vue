<template>
  <div
    v-if="visible"
    class="popup-selector-main overlay-menno h-full w-full z-40 fixed top-0 left-0 right-0 bottom-0 flex items-center justify-center"
  >
    <div class="max-w-50vw bg-dark rounded-lg relative p-1rem">
      <div class="text-gray-300 space-y-2 flex-auto flex-col text-center">
        <span class="text-xl text-gray-400 mb-1rem">Data Refresh in Progress</span><br/>
        <hr class="mt-1rem mb-1rem w-full">
        <span class="mt-0_5rem p-0_5rem text-lg">
          <a v-external-link target="_blank" class="url-link" href="https://github.com/menno-egginc/eggincdatacollection-docs/blob/main/DataEndpoints.md">Menno's Ship Data</a>
          is currently being loaded or refreshed.<br>
          This data is used to provide further insights to the drops of your ships,<br>
          and how "lucky" you've been.
          <span v-if="isAutoRefresh"><b>This task runs on EggLedger start-up once every 7 days.</b></span>
          <br/><br/>
          Depending on your internet speed, this may take a while.<br>
          This window will automatically close when the task has completed.
        </span>
        <hr class="mt-1rem mb-1rem w-full">
        <img :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  visible: boolean
  isAutoRefresh: boolean
}>()
</script>
