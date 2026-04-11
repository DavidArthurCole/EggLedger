<template>
  <div
    v-if="visible"
    class="top-click-detect popup-selector-main overlay-update w-full h-full z-40 fixed top-0 left-0 right-0 bottom-0 flex items-center justify-center"
  >
    <div class="inner-click-detect max-w-50vw bg-dark rounded-lg relative p-1rem">
      <button class="detect-trigger close-button" @click="$emit('close')">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
        </svg>
      </button>
      <div class="text-gray-300 space-y-2 flex-auto flex-col text-center">
        <span class="text-lg text-gray-400">New Version of EggLedger Available: <span class="text-green-500">{{ releaseTag }}</span></span><br/>
        <span>Update information:</span>
        <div
          v-if="releaseNotes !== ''"
          class="text-left flex-1 bg-darkerer overflow-auto gh-markdown-content max-h-60vh p-1rem rounded-md"
          v-html="renderedNotes"
        ></div>
        <div class="mt-1rem flex-auto justify-center items-center">
          <a v-external-link class="items-center justify-center text-gray-200 hover:text-gray-300" href="https://github.com/DavidArthurCole/EggLedger/releases/latest">
            <button class="min-w-30vw btn btn-outline-dark p-0_5rem pr-2rem pl-2rem rounded-md bg-blue-500 border-blue-600 hover:bg-blue-600 hover:border-blue-700">
              Download
            </button>
          </a>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { marked } from 'marked'

const props = defineProps<{
  visible: boolean
  releaseTag: string
  releaseNotes: string
}>()

defineEmits<{ close: [] }>()

const renderedNotes = computed(() => marked.parse(props.releaseNotes || '') as string)
</script>
