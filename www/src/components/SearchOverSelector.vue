<template>
  <div
    :class="'top-click-detect popup-selector-main overlay-' + (ledgerType ?? '') + (isLifetime ? '-lifetime' : '')"
    @click="clickTop"
    @keydown.esc="emit('close')"
  >
    <dialog class="inner-click-detect popup-selector-inner" open>
      <button class="detect-trigger close-button" @click.prevent="emit('close')">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <input
        :id="ledgerType + '-search' + (isLifetime ? '-lifetime' : '')"
        class="input-search"
        type="text"
        placeholder="Search..."
        @focus.prevent.stop="resetSelectedItem"
        @input="(e) => { e.preventDefault(); emit('input', (e.target as HTMLInputElement).value) }"
        @keydown="(e) => { if (e.key === 'ArrowUp' || e.key === 'ArrowDown') { e.preventDefault(); arrowKeyDown(e) } }"
      />
      <div class="popup-opt-container">
        <div
          v-for="item in itemList"
          :key="item.value"
          :id="item.value + '_div'"
          class="popup-opt"
          tabindex="0"
          @click.prevent="emit('select', item)"
          @keydown="(e) => { e.preventDefault(); if (e.key === 'ArrowUp' || e.key === 'ArrowDown') { arrowKeyDown(e) } else if (e.key === 'Enter') { emit('select', item); clearSearch() } }"
        >
          <img
            v-if="item.imagePath"
            :class="'max-w-7 mr-1rem' + (ledgerType === 'drop' ? ' rounded-full bg-r-' + item.rarity : '')"
            :alt="item.text"
            :src="getImgPath(item)"
          >
          <span :class="ledgerType === 'drop' ? 'text-rarity-' + item.rarity : 'text-gray-400'">
            {{ item.text }}
          </span>
        </div>
        <div v-if="itemList.length === 0">
          <span class="text-gray-400">No results found.</span>
        </div>
      </div>
    </dialog>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

interface SelectorItem {
  value: string
  text: string
  imagePath?: string
  rarity?: number
}

const props = withDefaults(defineProps<{
  itemList?: SelectorItem[]
  ledgerType?: string
  isLifetime?: boolean
}>(), {
  itemList: () => [],
  ledgerType: '',
  isLifetime: false,
})

const emit = defineEmits<{
  close: []
  input: [value: string]
  select: [item: SelectorItem]
}>()

const selectedItem = ref<SelectorItem | null>(null)

function searchInputId(): string {
  return (props.ledgerType ?? '') + '-search' + (props.isLifetime ? '-lifetime' : '')
}

function arrowKeyDown(event: KeyboardEvent) {
  if (event.key === 'ArrowUp') {
    if (selectedItem.value === null) return
    if (selectedItem.value === props.itemList[0]) {
      unselectItem(selectedItem.value)
      focusSearchBar()
    } else {
      const index = props.itemList.indexOf(selectedItem.value)
      unselectItem(selectedItem.value)
      selectItem(props.itemList[index - 1])
    }
  } else if (event.key === 'ArrowDown') {
    if (selectedItem.value === null) {
      selectItem(props.itemList[0])
    } else {
      const index = props.itemList.indexOf(selectedItem.value)
      if (index === props.itemList.length - 1) return
      unselectItem(selectedItem.value)
      selectItem(props.itemList[index + 1])
    }
  }
}

function clearSearch() {
  const el = document.getElementById(searchInputId()) as HTMLInputElement | null
  if (el) el.value = ''
}

function selectItem(item: SelectorItem) {
  document.getElementById(searchInputId())?.blur()
  selectedItem.value = item
  document.getElementById(item.value + '_div')!.classList.add('selected-popup-opt')
  document.getElementById(item.value + '_div')!.focus()
}

function unselectItem(item: SelectorItem) {
  selectedItem.value = null
  document.getElementById(item.value + '_div')!.classList.remove('selected-popup-opt')
  document.getElementById(item.value + '_div')!.blur()
}

function resetSelectedItem() {
  if (selectedItem.value === null) return
  unselectItem(selectedItem.value)
}

function focusSearchBar() {
  selectedItem.value = null
  document.getElementById(searchInputId())!.focus()
}

function getImgPath(item: SelectorItem): string {
  if (props.ledgerType === 'drop') return `images/${item.imagePath}`
  if (props.ledgerType === 'target') return `images/targets/${item.imagePath}`
  return item.imagePath ?? ''
}

function clickTop(e: MouseEvent) {
  const topElement = e.target as HTMLElement
  const innerEl = topElement.querySelector('.inner-click-detect')
  if (innerEl === null) return
  if (innerEl.classList.contains('hidden')) return
  const divRect = innerEl.getBoundingClientRect()
  if (
    e.clientX < divRect.left ||
    e.clientX > divRect.right ||
    e.clientY < divRect.top ||
    e.clientY > divRect.bottom
  ) {
    (innerEl.querySelector('.detect-trigger') as HTMLElement).click()
  }
}
</script>
