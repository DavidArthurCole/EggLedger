/**
 * useFilters.ts - Shared filter logic for MissionDataView and LifetimeDataView.
 *
 * Parameterized by idSuffix ('' for missions, 'lifetime' for lifetime view)
 * which drives all DOM element IDs and CSS selector suffixes. The accountId
 * ref is used when fetching ship drops for the 'drops' filter case.
 */

import { ref, watch } from 'vue'
import type { Ref } from 'vue'
import type { DatabaseMission, PossibleArtifact, PossibleMission, PossibleTarget } from '../types/bridge'
import { advancedDropFilter } from './useSettings'

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

  function artifactDisplayText(artifact: PossibleArtifact): string {
    const displayName = artifact.displayName
    const level = artifact.level
    let displayText = displayName
    if (String(level) !== '%') {
      const isStoneNotFragment =
        displayName.toLowerCase().includes('stone') &&
        !displayName.toLowerCase().includes('fragment')
      displayText += ' (T' + (Number(level) + (isStoneNotFragment ? 2 : 1)) + ')'
    }
    return displayText
  }

  function dropPath(drop: PossibleArtifact): string {
    const addendum = drop.protoName.includes('_STONE') ? 1 : 0
    const fixedName = drop.protoName
      .replaceAll('_FRAGMENT', '')
      .replaceAll('ORNATE_GUSSET', 'GUSSET')
      .replaceAll('VIAL_MARTIAN_DUST', 'VIAL_OF_MARTIAN_DUST')
    return 'artifacts/' + fixedName + '/' + fixedName + '_' + (drop.level + 1 + addendum) + '.png'
  }

  function dropRarityPath(drop: PossibleArtifact): string {
    switch (drop.rarity) {
      case 1: return 'images/rare.gif'
      case 2: return 'images/epic.gif'
      case 3: return 'images/legendary.gif'
      default: return ''
    }
  }

  function getFilterValueOptions(topLevel: string | null): FilterOption[] {
    switch (topLevel) {
      case 'ship':
        return Array.from({ length: 11 }, (_, index) => ({
          text: [
            'Chicken One', 'Chicken Nine', 'Chicken Heavy',
            'BCR', 'Quintillion Chicken', 'Cornish-Hen Corvette',
            'Galeggtica', 'Defihent', 'Voyegger', 'Henerprise', 'Atreggies Henliner',
          ][index],
          value: String(index),
        }))
      case 'farm':
        return Array.from({ length: 2 }, (_, index) => ({
          text: ['Home', 'Virtue'][index],
          value: String(index),
        }))
      case 'duration':
        return Array.from({ length: 4 }, (_, index) => ({
          text: ['Short', 'Standard', 'Extended', 'Tutorial'][index],
          value: String(index),
          styleClass: 'text-duration-' + index,
        }))
      case 'level':
        return Array.from({ length: 9 }, (_, index) => ({
          text: index + '\u2605',
          value: String(index),
        }))
      case 'target':
        return options.possibleTargets.value.map((target) => ({
          text: target.displayName,
          value: String(target.id),
          imagePath: target.imageString,
        }))
      case 'drops': {
        const artifactList = options.artifactConfigs.value
          .filter((a) => a.baseQuality <= options.maxQuality.value)

        const result: FilterOption[] = [
          { text: 'Any Rare', value: '%_%_1_%', rarity: 1, styleClass: 'text-rare', imagePath: 'icon_help.webp' },
          { text: 'Any Epic', value: '%_%_2_%', rarity: 2, styleClass: 'text-epic', imagePath: 'icon_help.webp' },
          { text: 'Any Legendary', value: '%_%_3_%', rarity: 3, styleClass: 'text-legendary', imagePath: 'icon_help.webp' },
        ]

        // Group by family (name) then by tier (level), preserving insertion order
        const byFamily = new Map<number, Map<number, PossibleArtifact[]>>()
        for (const a of artifactList) {
          if (!byFamily.has(a.name)) byFamily.set(a.name, new Map())
          const byTier = byFamily.get(a.name)!
          if (!byTier.has(a.level)) byTier.set(a.level, [])
          byTier.get(a.level)!.push(a)
        }

        for (const [familyId, tierMap] of byFamily) {
          if (advancedDropFilter.value) {
            const firstArtifact = [...tierMap.values()][0][0]
            result.push({
              text: firstArtifact.displayName + ' (Any)',
              value: familyId + '_%_%_%',
              rarity: 0,
              imagePath: dropPath(firstArtifact),
            })
          }
          for (const [tierLevel, rarities] of tierMap) {
            if (advancedDropFilter.value && rarities.some((a) => a.rarity > 0)) {
              const tierRep = rarities.find((a) => a.rarity === 0) ?? rarities[0]
              result.push({
                text: artifactDisplayText(rarities[0]) + ' (Any Rarity)',
                value: familyId + '_' + tierLevel + '_%_%',
                rarity: 0,
                imagePath: dropPath(tierRep),
              })
            }
            for (const a of rarities) {
              result.push({
                text: artifactDisplayText(a),
                value: a.name + '_' + a.level + '_' + a.rarity + '_' + a.baseQuality,
                rarity: a.rarity,
                imagePath: dropPath(a),
                rarityGif: dropRarityPath(a),
              })
            }
          }
        }

        return result
      }
      case 'type':
        return [
          { text: 'Home', value: '0' },
          { text: 'Virtue', value: '1' },
          { text: 'Unknown', value: '-1' },
        ]
      case 'buggedcap':
      case 'dubcap':
        return [{ text: 'True', value: 'true' }, { text: 'False', value: 'false' }]
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
      const opts = new Set(getFilterValueOptions(targetEl.value ?? '').map((o) => String(o.value)))
      if (!opts.has(String(filterOperators.value[index]))) filterOperators.value[index] = null
      if (!opts.has(String(filterValues.value[index]))) {
        filterValues.value[index] = null
        dFilterValues.value[index] = null
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
      const opts = new Set(getFilterValueOptions(targetEl.value ?? '').map((o) => String(o.value)))
      if (!opts.has(String(orFilterOperators.value[index][orIndex]))) {
        orFilterOperators.value[index][orIndex] = null
      }
      if (!opts.has(String(orFilterValues.value[index][orIndex]))) {
        orFilterValues.value[index][orIndex] = null
        dOrFilterValues.value[index][orIndex] = null
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

  // Mission-against-filter logic

  function commonFilterLogic(
    value: unknown,
    filterValue: unknown,
    operator: string,
    currentState: boolean,
  ): boolean {
    switch (operator) {
      case 'd=':
        if ((value as Date).toDateString() !== (filterValue as Date).toDateString()) return false
        break
      case '=':
        if (value != filterValue) return false
        break
      case '!=':
        if (value == filterValue) return false
        break
      case '>':
        if ((value as number) <= (filterValue as number)) return false
        break
      case '<':
        if ((value as number) >= (filterValue as number)) return false
        break
      case 'true':
        if (!value) return false
        break
      case 'false':
        if (value) return false
        break
      default:
        return currentState
    }
    return currentState
  }

  // eslint-disable-next-line sonarjs/cognitive-complexity
  async function testMissionAgainstFilter(
    mission: DatabaseMission,
    filter: FilterCondition,
  ): Promise<boolean> {
    let filterPassed = true
    if (filter.topLevel != null && filter.op != null && filter.val != null) {
      switch (filter.topLevel) {
        case 'buggedcap':
          filterPassed = commonFilterLogic(mission.isBuggedCap, null, filter.val, filterPassed)
          break
        case 'dubcap':
          filterPassed = commonFilterLogic(mission.isDubCap, null, filter.val, filterPassed)
          break
        case 'ship':
          filterPassed = commonFilterLogic(mission.ship, filter.val, filter.op, filterPassed)
          break
        case 'farm':
          filterPassed = commonFilterLogic(mission.missionType, filter.val, filter.op, filterPassed)
          break
        case 'duration':
          filterPassed = commonFilterLogic(mission.durationType, filter.val, filter.op, filterPassed)
          break
        case 'level':
          filterPassed = commonFilterLogic(mission.level, filter.val, filter.op, filterPassed)
          break
        case 'target':
          filterPassed = commonFilterLogic(mission.targetInt, filter.val, filter.op, filterPassed)
          break
        case 'type':
          filterPassed = commonFilterLogic(mission.missionType, filter.val, filter.op, filterPassed)
          break
        case 'launchDT':
          filterPassed = commonFilterLogic(
            ledgerDate(mission.launchDT),
            ledgerDateObj(filter.val),
            filter.op,
            filterPassed,
          )
          break
        case 'returnDT':
          filterPassed = commonFilterLogic(
            ledgerDate(mission.returnDT),
            ledgerDateObj(filter.val),
            filter.op,
            filterPassed,
          )
          break
        case 'drops': {
          const shipConfig = options.durationConfigs.value[mission.ship]
          if (!shipConfig) {
            filterPassed = false
            break
          }
          const durConfig = shipConfig.durations[mission.durationType]
          if (!durConfig) {
            filterPassed = false
            break
          }
          const maxQual = durConfig.maxQuality + durConfig.levelQualityBump * mission.level
          const filterQuality = filter.val.split('_')[3]
          const qualityBypass = filterQuality === '%'
          const filterRarity = filter.val.split('_')[2]
          const rarityBypass = filterRarity === '%'
          const filterLevel = filter.val.split('_')[1]
          const levelBypass = filterLevel === '%'
          const filterName = filter.val.split('_')[0]
          const nameBypass = filterName === '%'
          const allDrops = await globalThis.getShipDrops(options.accountId.value ?? '', mission.missionId)
          if (allDrops == null) {
            filterPassed = false
          } else {
            switch (filter.op) {
              case 'c': {
                let foundDrop = false
                if (
                  (!qualityBypass && maxQual < Number.parseFloat(filterQuality)) ||
                  (!qualityBypass && durConfig.minQuality > Number.parseFloat(filterQuality))
                ) {
                  filterPassed = false
                }
                for (const drop of allDrops) {
                  if (!nameBypass && Number.parseInt(filterName) !== drop.id) continue
                  if (!levelBypass && Number.parseInt(filterLevel) !== drop.level) continue
                  if (!rarityBypass && Number.parseInt(filterRarity) !== drop.rarity) continue
                  foundDrop = true
                }
                if (!foundDrop) filterPassed = false
                break
              }
              case 'dnc': {
                for (const drop of allDrops) {
                  if (!nameBypass && Number.parseInt(filterName) !== drop.id) continue
                  if (!levelBypass && Number.parseInt(filterLevel) !== drop.level) continue
                  if (!rarityBypass && Number.parseInt(filterRarity) !== drop.rarity) continue
                  filterPassed = false
                }
                break
              }
            }
          }
          break
        }
      }
      return filterPassed
    }
    return false
  }

  // eslint-disable-next-line sonarjs/cognitive-complexity
  async function missionMatchesFilter(
    mission: DatabaseMission,
    filters: FilterCondition[],
    orFilters: FilterCondition[][],
  ): Promise<boolean> {
    let allFiltersPassed = true
    let index = 0
    for (const filter of filters) {
      let filterPassed = true
      if (filter.topLevel != null && filter.op != null && (filter.val != null || filter.topLevel === 'target')) {
        filterPassed = await testMissionAgainstFilter(mission, filter)
        if (!filterPassed) {
          if (orFilters[index] != null) {
            for (const orFilter of orFilters[index]) {
              if (orFilter.topLevel != null && orFilter.op != null && (orFilter.val != null || orFilter.topLevel === 'target')) {
                filterPassed = await testMissionAgainstFilter(mission, orFilter)
                if (filterPassed) break
              }
            }
          }
          if (!filterPassed) allFiltersPassed = false
        }
      }
      index++
    }
    return allFiltersPassed
  }

  // Drop/target filter overlay state

  const dropSelectList = ref<FilterOption[]>([])
  const dropFilterSelectedIndex = ref<number | null>(null)
  const dropFilterSelectedOrIndex = ref<number | null>(null)
  const dropFilterMenuOpen = ref(false)
  const dropSearchTerm = ref('')

  const targetSelectList = ref<FilterOption[]>([])
  const targetFilterSelectedIndex = ref<number | null>(null)
  const targetFilterSelectedOrIndex = ref<number | null>(null)
  const targetFilterMenuOpen = ref(false)
  const targetSearchTerm = ref('')

  watch(dropSearchTerm, () => {
    dropSelectList.value = getFilterValueOptions('drops').filter((d) =>
      d.text.toLowerCase().includes(dropSearchTerm.value.toLowerCase()),
    )
  })
  watch(advancedDropFilter, () => {
    if (!dropFilterMenuOpen.value) return
    const all = getFilterValueOptions('drops')
    dropSelectList.value = dropSearchTerm.value
      ? all.filter((d) => d.text.toLowerCase().includes(dropSearchTerm.value.toLowerCase()))
      : all
  })
  watch(targetSearchTerm, () => {
    targetSelectList.value = getFilterValueOptions('target').filter((t) =>
      t.text.toLowerCase().includes(targetSearchTerm.value.toLowerCase()),
    )
  })

  function openDropFilterMenu(index: number, orIndex: number | null) {
    dropSelectList.value = getFilterValueOptions('drops')
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
