<template>
  <div class="relative" ref="containerRef">
    <button
      type="button"
      class="relative -bottom-px flex items-center gap-1.5 px-4 pt-2 pb-1.5 text-sm font-medium text-gray-400 border border-darker_tab border-300 rounded-t-md bg-darker_tab hover:bg-dark_tab_hover transition-colors whitespace-nowrap"
      @click="toggle"
    >
      <template v-if="activeAccount">
        <span :style="'color: #' + activeAccount.accountColor" class="font-medium">{{ activeAccount.nickname }}</span>
        <span class="text-gray-500 text-xs">{{ activeAccount.ebString }}</span>
      </template>
      <template v-else>
        <span class="text-gray-500 italic text-xs">No account - click to add</span>
      </template>
      <span class="text-gray-600 ml-1 text-xs">&#9660;</span>
    </button>

    <ul
      v-if="isOpen"
      class="absolute right-0 top-full z-50 mt-1 min-w-80 rounded-md border border-gray-600 bg-darkest shadow-xl text-xs"
    >
      <li
        v-for="acct in sortedAccounts"
        :key="acct.id"
        class="flex items-start gap-2 px-3 py-2.5 cursor-pointer hover:bg-darker transition-colors border-b border-gray-800 last:border-b-0"
        @click="select(acct.id)"
      >
        <span class="text-gray-500 w-3 flex-shrink-0 mt-0.5">
          <template v-if="acct.id === activeAccountId">&#10003;</template>
        </span>
        <span class="flex-1 min-w-0 flex flex-col gap-0.5">
          <span>
            <span v-if="screenshotSafety" class="text-gray-400">
              EI<span class="inline-block rounded-sm bg-current select-none" style="width: 10ch; height: 0.75em; vertical-align: -0.05em;" />
            </span>
            <span v-else class="text-gray-400">{{ acct.id }}</span>
          </span>
          <span class="flex items-center gap-1.5">
            <span :style="'color: #' + acct.accountColor" class="font-medium">{{ acct.nickname }}</span>
            <span class="text-gray-500">{{ acct.ebString }}</span>
          </span>
          <span v-if="accountDetails(acct.id)" class="flex items-center gap-1.5 text-gray-500">
            <template v-if="accountDetails(acct.id)?.seString">
              <img :src="'images/soul_egg.png'" style="display:inline;height:1em;vertical-align:middle;" alt="">
              <span style="color:#a855f7">{{ accountDetails(acct.id)?.seString }} SE</span>
              <span>·</span>
              <img :src="'images/prophecy_egg.png'" style="display:inline;height:1em;vertical-align:middle;" alt="">
              <span style="color:#eab308">{{ accountDetails(acct.id)?.peCount }} PE</span>
              <template v-if="accountDetails(acct.id)?.teCount">
                <span>·</span>
                <img :src="'images/truth_egg.png'" style="display:inline;height:1em;vertical-align:middle;" alt="">
                <span style="color:#c831ff">{{ accountDetails(acct.id)?.teCount }} TE</span>
              </template>
            </template>
          </span>
          <span class="text-gray-600">{{ acct.missionCount }} missions</span>
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
const { existingData, knownAccounts } = useAppState()

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

function accountDetails(id: string) {
  return knownAccounts.value?.find((a) => a.id === id) ?? null
}

function select(id: string) {
  setActive(id)
  close()
}

function openAddDialog() {
  close()
  addDialogRef.value?.open()
}

async function onAccountAdded(account: Account) {
  setActive(account.id)
  existingData.value = await globalThis.getExistingData()
}
</script>
