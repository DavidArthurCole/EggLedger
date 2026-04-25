<template>
  <dialog
    ref="dialogRef"
    class="rounded-lg bg-darker text-gray-200 border border-gray-600 p-0 shadow-xl backdrop:bg-black/50"
    @click.self="close"
  >
    <div class="p-5 min-w-80">
      <h2 class="text-sm font-semibold text-gray-200 mb-3">Add Account</h2>
      <form @submit.prevent="submit">
        <input
          v-model="eid"
          type="text"
          class="w-full px-2 py-1 text-sm rounded bg-darkest border border-gray-600 text-gray-200 focus:outline-none focus:border-blue-500 mb-2"
          placeholder="Enter player ID (e.g. EI...)"
          :disabled="loading"
          autofocus
        />
        <p v-if="error" class="text-xs text-red-500 mb-2">{{ error }}</p>
        <p v-if="loading" class="text-xs text-gray-400 mb-2">Fetching account data...</p>
        <div class="flex gap-2 justify-end">
          <button
            type="button"
            class="text-xs px-3 py-1 rounded border border-gray-600 text-gray-400 hover:text-gray-200"
            :disabled="loading"
            @click="close"
          >Cancel</button>
          <button
            type="submit"
            class="text-xs px-3 py-1 rounded bg-indigo-700 text-white hover:bg-indigo-600 disabled:opacity-50"
            :disabled="loading || eid.trim() === ''"
          >Add</button>
        </div>
      </form>
    </div>
  </dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Account } from '../types/bridge'

const emit = defineEmits<{
  added: [account: Account]
}>()

const dialogRef = ref<HTMLDialogElement | null>(null)
const eid = ref('')
const loading = ref(false)
const error = ref('')

function open() {
  eid.value = ''
  error.value = ''
  loading.value = false
  dialogRef.value?.showModal()
}

function close() {
  dialogRef.value?.close()
}

async function submit() {
  if (eid.value.trim() === '') return
  loading.value = true
  error.value = ''
  try {
    const account = await globalThis.addAccount(eid.value.trim())
    emit('added', account)
    close()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

defineExpose({ open, close })
</script>
