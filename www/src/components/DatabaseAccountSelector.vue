<template>
  <form class="select-form" @submit.prevent="onSubmit">
    <div ref="containerRef" class="relative flex-grow focus-within:z-10">
      <div v-if="selectedAccount != null" class="ledger-input-overlay" @click="openDropdown">
        <span class="whitespace-pre">
          <template v-if="screenshotSafety">EI<span class="inline-block rounded-sm bg-current select-none" style="width: 16ch; height: 0.8em; vertical-align: -0.05em;" /></template>
          <template v-else>{{ selectedAccount.id }}</template>
        </span>
        (<span :style="'color: #' + selectedAccount.accountColor">{{ selectedAccount.nickname }} {{ selectedAccount.ebString }}</span>
        - {{ selectedAccount.missionCount }} missions)
      </div>
      <input
        ref="inputRef"
        type="text"
        class="drop-select border-gray-300"
        :placeholder="placeholder"
        :value="inputValue"
        @focus="openDropdown"
        @input="(e) => onInput((e.target as HTMLInputElement).value)"
      />
      <ul v-if="isOpen && accounts.length > 0" class="ledger-list" tabindex="-1">
        <li
          v-for="acct in filteredAccounts"
          :key="acct.id"
          class="drop-opt"
          @mousedown.prevent="selectAccount(acct.id)"
        >
          <template v-if="screenshotSafety">EI<span class="inline-block rounded-sm bg-current select-none" style="width: 16ch; height: 0.8em; vertical-align: -0.05em;" /></template>
          <template v-else>{{ acct.id }}</template>
          (<span :style="'color: #' + acct.accountColor">{{ acct.nickname }} {{ acct.ebString }}</span>
          - {{ acct.missionCount }} missions)
        </li>
      </ul>
    </div>
    <button
      class="view-form-button"
      type="submit"
      :disabled="!selectedAccount || isLoading"
    >
      {{ buttonLabel }}
    </button>
  </form>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import type { DatabaseAccount } from '../types/bridge'
import { screenshotSafety } from '../composables/useSettings'

const props = withDefaults(defineProps<{
  accounts: DatabaseAccount[]
  buttonLabel?: string
  isLoading?: boolean
  placeholder?: string
}>(), {
  placeholder: 'Enter player ID or nickname...',
})

const emit = defineEmits<{
  submit: [id: string]
}>()

const containerRef = ref<HTMLElement | null>(null)
const inputRef = ref<HTMLInputElement | null>(null)
const isOpen = ref(false)
const selectedId = ref<string | null>(null)
const inputValue = ref('')

const selectedAccount = computed(() =>
  props.accounts.find(a => a.id === selectedId.value) ?? null,
)

const filteredAccounts = computed(() => {
  if (!inputValue.value || selectedId.value) return props.accounts
  const q = inputValue.value.toLowerCase()
  return props.accounts.filter(a =>
    a.id.toLowerCase().includes(q) || a.nickname.toLowerCase().includes(q),
  )
})

function openDropdown() {
  isOpen.value = true
  if (selectedId.value) inputValue.value = ''
}

function selectAccount(id: string) {
  selectedId.value = id
  inputValue.value = ''
  isOpen.value = false
}

function onInput(val: string) {
  inputValue.value = val
  selectedId.value = null
  isOpen.value = true
}

function onSubmit() {
  if (selectedAccount.value) {
    emit('submit', selectedAccount.value.id)
  }
}

function handleClickOutside(e: MouseEvent) {
  if (isOpen.value && containerRef.value && !containerRef.value.contains(e.target as Node)) {
    isOpen.value = false
    if (!selectedId.value) inputValue.value = ''
  }
}

onMounted(() => document.addEventListener('mousedown', handleClickOutside))
onUnmounted(() => document.removeEventListener('mousedown', handleClickOutside))
</script>
