<template>
  <div class="relative" ref="containerRef">
    <button
      type="button"
      class="flex items-center gap-1 px-2 py-1 text-xs rounded border border-gray-700 bg-darker hover:bg-dark_tab_hover text-gray-300 transition-colors"
      @click="toggle"
    >
      <template v-if="activeAccount">
        <span :style="'color: #' + activeAccount.accountColor" class="font-medium">{{ activeAccount.nickname }}</span>
        <span class="text-gray-500">{{ activeAccount.ebString }}</span>
      </template>
      <template v-else>
        <span class="text-gray-500 italic">No account - click to add</span>
      </template>
      <span class="text-gray-600 ml-1">&#9660;</span>
    </button>

    <ul
      v-if="isOpen"
      class="absolute right-0 top-full z-50 mt-1 min-w-56 rounded-md border border-gray-600 bg-darkest shadow-xl text-xs"
    >
      <li
        v-for="acct in sortedAccounts"
        :key="acct.id"
        class="flex items-center gap-2 px-3 py-2 cursor-pointer hover:bg-darker transition-colors"
        @click="select(acct.id)"
      >
        <span class="text-gray-500 w-3 flex-shrink-0">
          <template v-if="acct.id === activeAccountId">&#10003;</template>
        </span>
        <span class="flex-1 min-w-0">
          <span v-if="screenshotSafety" class="text-gray-400">
            EI<span class="inline-block rounded-sm bg-current select-none" style="width: 10ch; height: 0.75em; vertical-align: -0.05em;" />
          </span>
          <span v-else class="text-gray-400">{{ acct.id }}</span>
          <span class="ml-1">
            (<span :style="'color: #' + acct.accountColor">{{ acct.nickname }}</span>
            <span class="text-gray-500 ml-1">{{ acct.missionCount }} missions</span>)
          </span>
        </span>
      </li>
      <li
        class="flex items-center gap-2 px-3 py-2 cursor-pointer hover:bg-darker transition-colors border-t border-gray-700 text-gray-400"
        @click="openAddDialog"
      >
        <span class="w-3 flex-shrink-0" />
        + Add Account
      </li>
    </ul>
  </div>

  <AddAccountDialog ref="addDialogRef" @added="onAccountAdded" />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useDropdownSelector } from '../composables/useDropdownSelector'
import { useActiveAccount } from '../composables/useActiveAccount'
import { useAppState } from '../composables/useAppState'
import { screenshotSafety } from '../composables/useSettings'
import AddAccountDialog from './AddAccountDialog.vue'
import type { Account } from '../types/bridge'

const { activeAccountId, setActive } = useActiveAccount()
const { existingData } = useAppState()

const addDialogRef = ref<InstanceType<typeof AddAccountDialog> | null>(null)

const { containerRef, isOpen, open, close } = useDropdownSelector()

function toggle() {
  if (isOpen.value) close()
  else open()
}

const sortedAccounts = computed(() =>
  [...existingData.value].sort((a, b) => b.missionCount - a.missionCount),
)

const activeAccount = computed(() =>
  existingData.value.find((a) => a.id === activeAccountId.value) ?? null,
)

function select(id: string) {
  setActive(id)
  close()
}

function openAddDialog() {
  close()
  addDialogRef.value?.open()
}

function onAccountAdded(account: Account) {
  setActive(account.id)
  void globalThis.getExistingData().then((data) => {
    existingData.value = data
  })
}
</script>
