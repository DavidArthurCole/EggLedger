<template>
  <div class="text-center mt-3 rounded-md border border-red-700 py-2">
    <span class="text-red-700">
      No mission data has been loaded yet.<br />Please <b>Fetch</b> from the
    </span>
    <button type="button" class="btn-link" @click="$emit('navigate', 'Ledger')">
      <span class="text-blue-500 ledger-underline font-bold">Ledger tab</span>
    </button>
    <span class="text-red-700"> and then come back here.</span>
  </div>
</template>

<script setup lang="ts">
defineEmits<{ navigate: [tab: string] }>()
</script>
