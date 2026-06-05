/**
 * useFilterOverlays.ts - Drop and target modal picker overlays for useFilters.
 *
 * Extracted from useFilters.ts as a cohesive UI cluster: the searchable drop
 * and target overlay menus. These pickers are not fully standalone - selecting
 * a value writes back into the parallel-array filter state (filterValues,
 * dFilterValues, orFilterValues, dOrFilterValues) and re-runs the core change
 * handlers. Those touched refs and handlers are received via deps so the core
 * state model in useFilters is unchanged.
 *
 * The overlay watches register at setup time (this composable runs synchronously
 * inside useFilters' body), so watch timing is identical to the inlined version.
 */

import { ref, watch } from 'vue'
import type { Ref } from 'vue'
import { advancedDropFilter } from './useSettings'
import type { FilterOption } from './useFilters'

export interface UseFilterOverlaysDeps {
  idSuffixToken: string
  filterValues: Ref<(string | null)[]>
  dFilterValues: Ref<(string | null)[]>
  orFilterValues: Ref<(string | null)[][]>
  dOrFilterValues: Ref<(string | null)[][]>
  handleFilterChange: (eventOrId: Event | string) => void
  handleOrFilterChange: (eventOrId: Event | string) => void
  getFilterValueOptions: (topLevel: string | null) => FilterOption[]
}

export function useFilterOverlays(deps: UseFilterOverlaysDeps) {
  const {
    idSuffixToken,
    filterValues,
    dFilterValues,
    orFilterValues,
    dOrFilterValues,
    handleFilterChange,
    handleOrFilterChange,
    getFilterValueOptions,
  } = deps

  const dropSelectList = ref<FilterOption[]>([])
  const dropFilterSelectedIndex = ref<number | null>(null)
  const dropFilterSelectedOrIndex = ref<number | null>(null)
  const dropFilterMenuOpen = ref(false)
  const dropSearchTerm = ref('')

  // Base (unfiltered) drop options, rebuilt only when the menu opens or the
  // advanced-grouping toggle changes - NOT on every keystroke. getFilterValueOptions('drops')
  // walks the whole artifact config, so search filters this cache instead.
  let dropBaseList: FilterOption[] = []
  function filterDropBase(term: string): FilterOption[] {
    if (!term) return dropBaseList
    const lower = term.toLowerCase()
    return dropBaseList.filter((d) => d.text.toLowerCase().includes(lower))
  }

  const targetSelectList = ref<FilterOption[]>([])
  const targetFilterSelectedIndex = ref<number | null>(null)
  const targetFilterSelectedOrIndex = ref<number | null>(null)
  const targetFilterMenuOpen = ref(false)
  const targetSearchTerm = ref('')

  watch(dropSearchTerm, () => {
    dropSelectList.value = filterDropBase(dropSearchTerm.value)
  })
  watch(advancedDropFilter, () => {
    if (!dropFilterMenuOpen.value) return
    dropBaseList = getFilterValueOptions('drops')
    dropSelectList.value = filterDropBase(dropSearchTerm.value)
  })
  watch(targetSearchTerm, () => {
    targetSelectList.value = getFilterValueOptions('target').filter((t) =>
      t.text.toLowerCase().includes(targetSearchTerm.value.toLowerCase()),
    )
  })

  function openDropFilterMenu(index: number, orIndex: number | null) {
    dropBaseList = getFilterValueOptions('drops')
    dropSelectList.value = dropBaseList
    dropFilterSelectedIndex.value = index
    dropFilterSelectedOrIndex.value = orIndex ?? null
    dropFilterMenuOpen.value = true
    const el = document.querySelector('.overlay-drop' + idSuffixToken) as HTMLElement | null
    if (el) {
      el.style.display = 'flex'
      el.classList.remove('hidden')
    }
    const input = document.getElementById('drop-search' + idSuffixToken) as HTMLInputElement | null
    input?.focus()
  }

  function closeDropFilterMenu() {
    dropSearchTerm.value = ''
    dropFilterSelectedIndex.value = null
    dropFilterSelectedOrIndex.value = null
    dropFilterMenuOpen.value = false
    const el = document.querySelector('.overlay-drop' + idSuffixToken) as HTMLElement | null
    if (el) {
      el.style.display = 'none'
      el.classList.add('hidden')
    }
  }

  function selectDropFilter(drop: FilterOption) {
    const newValue = drop.value.toString().split('.')[0]
    if (dropFilterSelectedIndex.value != null) {
      if (dropFilterSelectedOrIndex.value == null) {
        filterValues.value[dropFilterSelectedIndex.value] = newValue
        dFilterValues.value[dropFilterSelectedIndex.value] = drop.text
        handleFilterChange(`filter-value-${dropFilterSelectedIndex.value}${idSuffixToken}`)
        const el = document.getElementById(
          `filter-value-${dropFilterSelectedIndex.value}${idSuffixToken}`,
        ) as HTMLInputElement | null
        if (el) el.size = drop.text.length
      } else {
        orFilterValues.value[dropFilterSelectedIndex.value][dropFilterSelectedOrIndex.value] = newValue
        dOrFilterValues.value[dropFilterSelectedIndex.value][dropFilterSelectedOrIndex.value] = drop.text
        handleOrFilterChange(
          `filter-value-${dropFilterSelectedIndex.value}-${dropFilterSelectedOrIndex.value}${idSuffixToken}`,
        )
        const el = document.getElementById(
          `filter-value-${dropFilterSelectedIndex.value}-${dropFilterSelectedOrIndex.value}${idSuffixToken}`,
        ) as HTMLInputElement | null
        if (el) el.size = drop.text.length
      }
    }
    closeDropFilterMenu()
  }

  function openTargetFilterMenu(index: number, orIndex: number | null) {
    targetSelectList.value = getFilterValueOptions('target')
    targetFilterSelectedIndex.value = index
    targetFilterSelectedOrIndex.value = orIndex ?? null
    targetFilterMenuOpen.value = true
    const el = document.querySelector('.overlay-target' + idSuffixToken) as HTMLElement | null
    if (el) {
      el.style.display = 'flex'
      el.classList.remove('hidden')
    }
    const input = document.getElementById('target-search' + idSuffixToken) as HTMLInputElement | null
    input?.focus()
  }

  function closeTargetFilterMenu() {
    targetSearchTerm.value = ''
    targetFilterSelectedIndex.value = null
    targetFilterSelectedOrIndex.value = null
    targetFilterMenuOpen.value = false
    const el = document.querySelector('.overlay-target' + idSuffixToken) as HTMLElement | null
    if (el) {
      el.style.display = 'none'
      el.classList.add('hidden')
    }
  }

  function selectTargetFilter(target: FilterOption) {
    const newValue = target.value.toString().split('.')[0]
    if (targetFilterSelectedIndex.value != null) {
      if (targetFilterSelectedOrIndex.value == null) {
        filterValues.value[targetFilterSelectedIndex.value] = newValue
        dFilterValues.value[targetFilterSelectedIndex.value] = target.text
        handleFilterChange(`filter-value-${targetFilterSelectedIndex.value}${idSuffixToken}`)
        const el = document.getElementById(
          `filter-value-${targetFilterSelectedIndex.value}${idSuffixToken}`,
        ) as HTMLInputElement | null
        if (el) el.size = target.text.length
      } else {
        orFilterValues.value[targetFilterSelectedIndex.value][targetFilterSelectedOrIndex.value] = newValue
        dOrFilterValues.value[targetFilterSelectedIndex.value][targetFilterSelectedOrIndex.value] = target.text
        handleOrFilterChange(
          `filter-value-${targetFilterSelectedIndex.value}-${targetFilterSelectedOrIndex.value}${idSuffixToken}`,
        )
        const el = document.getElementById(
          `filter-value-${targetFilterSelectedIndex.value}-${targetFilterSelectedOrIndex.value}${idSuffixToken}`,
        ) as HTMLInputElement | null
        if (el) el.size = target.text.length
      }
    }
    closeTargetFilterMenu()
  }

  return {
    dropSelectList,
    dropFilterSelectedIndex,
    dropFilterSelectedOrIndex,
    dropFilterMenuOpen,
    dropSearchTerm,
    targetSelectList,
    targetFilterSelectedIndex,
    targetFilterSelectedOrIndex,
    targetFilterMenuOpen,
    targetSearchTerm,
    openDropFilterMenu,
    closeDropFilterMenu,
    selectDropFilter,
    openTargetFilterMenu,
    closeTargetFilterMenu,
    selectTargetFilter,
  }
}
