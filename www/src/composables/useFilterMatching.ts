/**
 * useFilterMatching.ts - Client-side mission-against-filter testing for useFilters.
 *
 * Extracted from useFilters.ts as a self-contained cluster. Tests a
 * DatabaseMission against a compiled filter tree (AND conditions with OR
 * siblings). The drops case is async because it fetches drop data via
 * globalThis.getShipDrops. Date helpers (ledgerDate/ledgerDateObj) are passed
 * in because useFilters also exposes them to its view consumers.
 */

import type { Ref } from 'vue'
import type { DatabaseMission, PossibleMission } from '../types/bridge'
import type { FilterCondition } from './useFilters'

export interface UseFilterMatchingDeps {
  durationConfigs: Ref<PossibleMission[]>
  accountId: Ref<string | null>
  ledgerDate: (timestamp: number) => Date
  ledgerDateObj: (date: string) => Date
}

export function useFilterMatching(deps: UseFilterMatchingDeps) {
  const { durationConfigs, accountId, ledgerDate, ledgerDateObj } = deps

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
          const shipConfig = durationConfigs.value[mission.ship]
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
          const allDrops = await globalThis.getShipDrops(accountId.value ?? '', mission.missionId)
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

  return {
    testMissionAgainstFilter,
    missionMatchesFilter,
  }
}
