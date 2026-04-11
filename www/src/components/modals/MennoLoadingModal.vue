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
        <div v-if="progress" class="text-sm text-gray-400 tabular-nums">
          <div class="mb-1">
            <span>{{ formatBytes(progress.bytesRead) }}</span>
            <span v-if="progress.totalBytes > 0"> / {{ formatBytes(progress.totalBytes) }}</span>
            <span v-if="progress.speedBps > 0">  ({{ formatBytes(progress.speedBps) }}/s)</span>
            <span v-if="progress.etaSeconds >= 0">  ETA {{ Math.ceil(progress.etaSeconds) }}s</span>
          </div>
          <div class="h-3 relative rounded-full overflow-hidden">
            <div class="w-full h-full bg-darker absolute"></div>
            <div
              class="h-full absolute rounded-full bg-green-500 transition-all"
              :style="{ width: progress.totalBytes > 0 ? (progress.bytesRead / progress.totalBytes * 100).toFixed(1) + '%' : '100%' }"
            ></div>
          </div>
        </div>
        <img v-else :src="'images/loading.gif'" alt="Loading..." class="xl-ico" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { MennoDownloadProgress } from '../../types/bridge'

defineProps<{
  visible: boolean
  isAutoRefresh: boolean
  progress: MennoDownloadProgress | null
}>()

function formatBytes(bytes: number): string {
  if (bytes >= 1_000_000) return (bytes / 1_000_000).toFixed(1) + ' MB'
  if (bytes >= 1_000) return (bytes / 1_000).toFixed(0) + ' KB'
  return bytes.toFixed(0) + ' B'
}
</script>
