/**
 * useFilters.ts - Shared filter logic for MissionDataView and LifetimeDataView.
 *
 * Parameterized by idSuffix ('' for missions, 'lifetime' for lifetime view)
 * which drives all DOM element IDs and CSS selector suffixes. The accountId
 * ref is used when fetching ship drops for the 'drops' filter case.
 */

import { ref, watch } from 'vue'
import type { Ref } from 'vue'
import type { PossibleArtifact, PossibleMission, PossibleTarget } from '../types/bridge'
import { advancedDropFilter } from './useSettings'
import {
  getMissionFilterValueOptions as getSharedMissionFilterOptions,
  getTargetFilterOptions,
  getDropFilterOptions,
} from '../utils/filterOptions'
import { useFilterMatching } from './useFilterMatching'
import { useFilterOverlays } from './useFilterOverlays'

export interface FilterCondition {
  topLevel: string
  op: string
  val: string
}

export interface FilterOption {
  text: string
  value: string
  styleClass?: string
  imagePath?: string
  rarity?: number
  rarityGif?: string
}

interface HandleFilterChangeEventTarget extends HTMLElement {
  oldValue?: string | null
  value?: string
  type?: string
}

export interface UseFiltersOptions {
  /** Element ID suffix, e.g. 'lifetime'. Omit or pass '' for no suffix. */
  idSuffix?: string
  /** The currently loaded account ID, used for drop-filter bridge calls. */
  accountId: Ref<string | null>
  durationConfigs: Ref<PossibleMission[]>
  possibleTargets: Ref<PossibleTarget[]>
  maxQuality: Ref<number>
  artifactConfigs: Ref<PossibleArtifact[]>
}

export function useFilters(options: UseFiltersOptions) {
  const idSuffixToken = options.idSuffix ? '-' + options.idSuffix : ''

  // Strip the suffix token's segments from a split ID array for index parsing.
  // e.g. with idSuffixToken='-lifetime': ['filter','value','0','lifetime'] -> ['filter','value','0']
  function stripSuffixFromParts(parts: string[]): string[] {
    if (!idSuffixToken) return parts
    const suffixSegments = new Set(idSuffixToken.split('-').filter(Boolean))
    return parts.filter((p) => !suffixSegments.has(p))
  }

  // Filter value option helpers

  function getFilterValueOptions(topLevel: string | null): FilterOption[] {
    if (!topLevel) return []
    const shared = getSharedMissionFilterOptions(topLevel)
    if (shared.length > 0) return shared
    switch (topLevel) {
      case 'target':
        return getTargetFilterOptions(options.possibleTargets.value)
      case 'drops':
        return getDropFilterOptions(
          options.artifactConfigs.value,
          options.maxQuality.value,
          advancedDropFilter.value,
        )
      default:
        return []
    }
  }

  // Filter state

  const filterConditionsCount = ref(1)
  const filterTopLevel = ref<(string | null)[]>([])
  const filterOperators = ref<(string | null)[]>([])
  const filterValues = ref<(string | null)[]>([])

  const orFilterConditionsCount = ref<(number | null)[]>([])
  const orFilterTopLevel = ref<(string | null)[][]>([[]])
  const orFilterOperators = ref<(string | null)[][]>([[]])
  const orFilterValues = ref<(string | null)[][]>([[]])

  const dFilterValues = ref<(string | null)[]>([])
  const dOrFilterValues = ref<(string | null)[][]>([[]])

  const dataFilter = ref<FilterCondition[]>([])
  const orDataFilter = ref<FilterCondition[][]>([[]])
  const filterHasChanged = ref(false)

  watch(filterValues, () => {
    dFilterValues.value = filterValues.value.map(
      (_f, index) => (dFilterValues.value[index] === undefined ? '' : dFilterValues.value[index]),
    )
  }, { deep: true })

  watch(orFilterValues, () => {
    dOrFilterValues.value = orFilterValues.value.map((orFilter, index) =>
      dOrFilterValues.value[index] === undefined
        ? []
        : dOrFilterValues.value[index].slice(0, orFilter?.length ?? 0),
    )
  }, { deep: true })

  watch([dataFilter, orDataFilter], () => {
    filterHasChanged.value = true
  })

  // Filter handlers

  function changeFilterValue(
    index: number | string,
    orIndex: number | string | null,
    level: string,
    value: string,
  ) {
    const intIndex = typeof index === 'number' ? index : Number.parseInt(index)
    let intOrIndex: number | null = null
    if (orIndex != null) {
      intOrIndex = typeof orIndex === 'number' ? orIndex : Number.parseInt(orIndex)
    }
    switch (level) {
      case 'top':
        if (intOrIndex == null) filterTopLevel.value[intIndex] = value
        else orFilterTopLevel.value[intIndex][intOrIndex] = value
        break
      case 'operator':
        if (intOrIndex == null) filterOperators.value[intIndex] = value
        else orFilterOperators.value[intIndex][intOrIndex] = value
        break
      case 'value':
        if (intOrIndex == null) filterValues.value[intIndex] = value
        else orFilterValues.value[intIndex][intOrIndex] = value
        break
    }
  }

  // eslint-disable-next-line sonarjs/cognitive-complexity
  function updateValueStyling(passedEl: HTMLElement, isOr: boolean) {
    const trimmed = stripSuffixFromParts(passedEl.id.split('-'))
    const index = Number.parseInt(trimmed[2])
    const orIndex = isOr ? Number.parseInt(trimmed[trimmed.length - 1]) : null
    if (isOr && orIndex == null) return
    const el = document.getElementById(
      'filter-value-' + index + (isOr ? '-' + orIndex : '') + idSuffixToken,
    )
    if (!el) return
    const classMatchBorder = el.className.match(/ border-(.*?)(\s|$)/)
    const classMatchText = el.className.replace('text-sm', ' ').match(/ text-(.*?)(\s|$)/)
    const existingBorder = classMatchBorder ? 'border-' + classMatchBorder[1].trim() : ''
    const existingText = classMatchText ? 'text-' + classMatchText[1].trim() : ''
    const isDrops = isOr
      ? orFilterTopLevel.value[index][orIndex!] === 'drops'
      : filterTopLevel.value[index] === 'drops'
    const dropsValue = isOr ? orFilterValues.value[index][orIndex!] : filterValues.value[index]
    const isDuration = isOr
      ? orFilterTopLevel.value[index][orIndex!] === 'duration'
      : filterTopLevel.value[index] === 'duration'
    let newBorder = ''
    let newText = ''

    if (isDrops && dropsValue != null && dropsValue !== '') {
      switch (dropsValue.split('_')[2]) {
        case '1': newBorder = 'border-rare'; newText = 'text-rarity-1'; break
        case '2': newBorder = 'border-epic'; newText = 'text-rarity-2'; break
        case '3': newBorder = 'border-legendary'; newText = 'text-rarity-3'; break
        default: newBorder = 'border-gray-300'; newText = 'text-gray-400'; break
      }
    } else if (isDuration && (el as HTMLInputElement).value !== '') {
      switch ((el as HTMLInputElement).value) {
        case '0': newBorder = 'border-short'; newText = 'text-duration-0'; break
        case '1': newBorder = 'border-standard'; newText = 'text-duration-1'; break
        case '2': newBorder = 'border-extended'; newText = 'text-duration-2'; break
        case '3': newBorder = 'border-tutorial'; newText = 'text-duration-3'; break
        default: newBorder = 'border-gray-300'; newText = 'text-gray-400'; break
      }
    } else {
      newBorder = 'border-gray-300'
      newText = 'text-gray-400'
    }
    if (existingBorder) el.classList.replace(existingBorder, newBorder)
    else el.classList.add(newBorder)
    if (existingText) el.classList.replace(existingText, newText)
    else el.classList.add(newText)
  }

  // eslint-disable-next-line sonarjs/cognitive-complexity
  function updateFilterNoReCount() {
    filterHasChanged.value = true
    const newDataFilter: FilterCondition[] = []
    if (filterTopLevel.value.length !== 0 && filterTopLevel.value != null) {
      for (let i = 0; i < filterTopLevel.value.length; i++) {
        const topLevel = filterTopLevel.value[i]
        const op = filterOperators.value[i]
        const val = filterValues.value[i]
        if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) break
        newDataFilter.push({ topLevel, op, val })
      }
    }
    dataFilter.value = newDataFilter

    const newOrDataFilter: FilterCondition[][] = []
    for (let i = 0; i < orFilterConditionsCount.value.length; i++) {
      const arr: FilterCondition[] = []
      if (
        orFilterTopLevel.value[i] == null ||
        orFilterOperators.value[i] == null ||
        orFilterValues.value[i] == null
      ) continue
      for (let j = 0; j < orFilterTopLevel.value[i].length; j++) {
        const topLevel = orFilterTopLevel.value[i][j]
        const op = orFilterOperators.value[i][j]
        const val = orFilterValues.value[i][j]
        if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) break
        arr.push({ topLevel, op, val })
      }
      newOrDataFilter.push(arr)
    }
    orDataFilter.value = newOrDataFilter
  }

  function updateFilterConditionsCount() {
    filterHasChanged.value = true
    const newDataFilter: FilterCondition[] = []
    if (filterTopLevel.value.length === 0 || filterTopLevel.value == null) return
    let maxIndex = 0
    let set = false

    for (let i = 0; i < filterTopLevel.value.length; i++) {
      const topLevel = filterTopLevel.value[i]
      const op = filterOperators.value[i]
      const val = filterValues.value[i]

      if (topLevel == null || op == null || val == null || (topLevel.includes('DT') && val === '')) {
        maxIndex = i
        break
      }
      newDataFilter.push({ topLevel, op, val })
      if (i + 1 === filterConditionsCount.value) {
        filterConditionsCount.value += 1
        set = true
      }
    }
    if (maxIndex === 0) {
      maxIndex = filterTopLevel.value.filter((f) => f != null && f !== '').length
    }
    if (!set) filterConditionsCount.value = maxIndex + 1
    dataFilter.value = newDataFilter
  }

  function handleFilterChange(eventOrId: Event | string) {
    const targetEl =
      typeof eventOrId === 'string'
        ? (document.getElementById(eventOrId) as HandleFilterChangeEventTarget | null)
        : ((eventOrId.target as HandleFilterChangeEventTarget) ?? null)
    if (!targetEl) return
    const targetId = targetEl.id
    if (targetEl.oldValue == null || targetEl.type === 'date') {
      updateFilterConditionsCount()
      targetEl.oldValue = targetEl.value ?? ''
    } else updateFilterNoReCount()
    updateValueStyling(targetEl, false)
    if (targetId.includes('filter-top')) {
      const parsedParts = stripSuffixFromParts(targetId.split('-'))
      const index = Number.parseInt(parsedParts[parsedParts.length - 1])
      const newTop = targetEl.value ?? ''
      if (newTop === 'dubcap' || newTop === 'buggedcap') {
        filterOperators.value[index] = '='
      } else {
        const opts = new Set(getFilterValueOptions(newTop).map((o) => String(o.value)))
        if (!opts.has(String(filterOperators.value[index]))) filterOperators.value[index] = null
        if (!opts.has(String(filterValues.value[index]))) {
          filterValues.value[index] = null
          dFilterValues.value[index] = null
        }
      }
    }
  }

  function handleOrFilterChange(eventOrId: Event | string) {
    const targetEl =
      typeof eventOrId === 'string'
        ? (document.getElementById(eventOrId) as HandleFilterChangeEventTarget | null)
        : ((eventOrId.target as HandleFilterChangeEventTarget) ?? null)
    if (!targetEl) return
    const targetId = targetEl.id
    const parsedParts = stripSuffixFromParts(targetId.split('-'))
    const index = Number.parseInt(parsedParts[2])
    const orIndex = Number.parseInt(parsedParts[parsedParts.length - 1])
    updateFilterNoReCount()
    updateValueStyling(targetEl, true)
    if (targetId.includes('filter-top')) {
      const newTop = targetEl.value ?? ''
      if (newTop === 'dubcap' || newTop === 'buggedcap') {
        orFilterOperators.value[index][orIndex] = '='
      } else {
        const opts = new Set(getFilterValueOptions(newTop).map((o) => String(o.value)))
        if (!opts.has(String(orFilterOperators.value[index][orIndex]))) {
          orFilterOperators.value[index][orIndex] = null
        }
        if (!opts.has(String(orFilterValues.value[index][orIndex]))) {
          orFilterValues.value[index][orIndex] = null
          dOrFilterValues.value[index][orIndex] = null
        }
      }
    }
  }

  function addOr(index: number) {
    if (orFilterConditionsCount.value == null) orFilterConditionsCount.value = []
    if (orFilterConditionsCount.value[index] == null) orFilterConditionsCount.value[index] = 1
    else orFilterConditionsCount.value[index] = (orFilterConditionsCount.value[index] as number) + 1

    if (orFilterTopLevel.value[index] == null) orFilterTopLevel.value[index] = []
    if (orFilterOperators.value[index] == null) orFilterOperators.value[index] = []
    if (orFilterValues.value[index] == null) orFilterValues.value[index] = []

    orFilterTopLevel.value[index].push(null)
    orFilterOperators.value[index].push(null)
    orFilterValues.value[index].push(null)
  }

  function removeAndShift(index: number) {
    for (let i = index; i < filterTopLevel.value.length; i++) {
      filterTopLevel.value[i] = filterTopLevel.value[i + 1]
      filterOperators.value[i] = filterOperators.value[i + 1]
      filterValues.value[i] = filterValues.value[i + 1]
      dFilterValues.value[i] = dFilterValues.value[i + 1]

      orFilterConditionsCount.value[i] = orFilterConditionsCount.value[i + 1]
      orFilterTopLevel.value[i] = orFilterTopLevel.value[i + 1]
      orFilterOperators.value[i] = orFilterOperators.value[i + 1]
      orFilterValues.value[i] = orFilterValues.value[i + 1]
      dOrFilterValues.value[i] = dOrFilterValues.value[i + 1]
    }
    updateFilterConditionsCount()
    updateFilterNoReCount()
    filterHasChanged.value = true

    setTimeout(() => {
      const selector = idSuffixToken
        ? `[id^='filter-top-'][id$='${idSuffixToken}']`
        : "[id^='filter-top-']"
      document.querySelectorAll(selector).forEach((el) =>
        updateValueStyling(el as HTMLElement, false),
      )
    }, 10)
  }

  function removeOrAndShift(index: number, orIndex: number) {
    for (let i = orIndex; i < orFilterTopLevel.value[index].length; i++) {
      orFilterTopLevel.value[index][i] = orFilterTopLevel.value[index][i + 1]
      orFilterOperators.value[index][i] = orFilterOperators.value[index][i + 1]
      orFilterValues.value[index][i] = orFilterValues.value[index][i + 1]
      dOrFilterValues.value[index][i] = dOrFilterValues.value[index][i + 1]
    }
    const cur = orFilterConditionsCount.value[index]
    if (cur != null) {
      const next = cur - 1
      orFilterConditionsCount.value[index] = next === 0 ? null : next
    }
    updateFilterNoReCount()
    filterHasChanged.value = true
  }

  function generateFilterConditionsArr() {
    return new Array(filterConditionsCount.value)
  }

  function filterModVals() {
    return {
      top: filterTopLevel.value,
      operator: filterOperators.value,
      value: filterValues.value,
      dValue: dFilterValues.value,
      orTop: orFilterTopLevel.value,
      orOperator: orFilterOperators.value,
      orValue: orFilterValues.value,
      orDValue: dOrFilterValues.value,
      orCount: orFilterConditionsCount.value,
    }
  }

  function clearFilter() {
    filterTopLevel.value = []
    filterOperators.value = []
    filterValues.value = []
    filterConditionsCount.value = 1
    orFilterConditionsCount.value = []
    orFilterTopLevel.value = [[]]
    orFilterOperators.value = [[]]
    orFilterValues.value = [[]]
    dFilterValues.value = []
    dOrFilterValues.value = [[]]
    dataFilter.value = []
    orDataFilter.value = [[]]
    filterHasChanged.value = false
  }

  // Date helpers (also used by mission-matching logic)

  function ledgerDate(timestamp: number): Date {
    return new Date(timestamp * 1000)
  }

  function ledgerDateObj(date: string): Date {
    const parts = date.split('-')
    return new Date(Number.parseInt(parts[0]), Number.parseInt(parts[1]) - 1, Number.parseInt(parts[2]))
  }

  // Mission-against-filter logic (extracted to useFilterMatching)

  const { missionMatchesFilter } = useFilterMatching({
    durationConfigs: options.durationConfigs,
    accountId: options.accountId,
    ledgerDate,
    ledgerDateObj,
  })

  // Drop/target filter overlay state (extracted to useFilterOverlays)

  const {
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
  } = useFilterOverlays({
    idSuffixToken,
    filterValues,
    dFilterValues,
    orFilterValues,
    dOrFilterValues,
    handleFilterChange,
    handleOrFilterChange,
    getFilterValueOptions,
  })

  return {
    // Filter state
    filterConditionsCount,
    filterTopLevel,
    filterOperators,
    filterValues,
    orFilterConditionsCount,
    orFilterTopLevel,
    orFilterOperators,
    orFilterValues,
    dFilterValues,
    dOrFilterValues,
    dataFilter,
    orDataFilter,
    filterHasChanged,
    // Filter handlers
    getFilterValueOptions,
    changeFilterValue,
    updateValueStyling,
    updateFilterNoReCount,
    updateFilterConditionsCount,
    handleFilterChange,
    handleOrFilterChange,
    addOr,
    removeAndShift,
    removeOrAndShift,
    generateFilterConditionsArr,
    filterModVals,
    clearFilter,
    // Date helpers
    ledgerDate,
    ledgerDateObj,
    // Mission matching
    missionMatchesFilter,
    // Drop overlay state
    dropSelectList,
    dropFilterSelectedIndex,
    dropFilterSelectedOrIndex,
    dropFilterMenuOpen,
    dropSearchTerm,
    // Target overlay state
    targetSelectList,
    targetFilterSelectedIndex,
    targetFilterSelectedOrIndex,
    targetFilterMenuOpen,
    targetSearchTerm,
    // Overlay handlers
    openDropFilterMenu,
    closeDropFilterMenu,
    selectDropFilter,
    openTargetFilterMenu,
    closeTargetFilterMenu,
    selectTargetFilter,
  }
}
