import type { PossibleTarget } from '../types/bridge'

export interface FilterOption {
  text: string
  value: string
  styleClass?: string
  imagePath?: string
  rarity?: number
}

const SHIP_NAMES = [
  'Chicken One', 'Chicken Nine', 'Chicken Heavy',
  'BCR', 'Quintillion Chicken', 'Cornish-Hen Corvette',
  'Galeggtica', 'Defihent', 'Voyegger', 'Henerprise', 'Atreggies Henliner',
]

const DURATION_NAMES = ['Short', 'Standard', 'Extended', 'Tutorial']

export function getShipFilterOptions(): FilterOption[] {
  return SHIP_NAMES.map((text, i) => ({ text, value: String(i) }))
}

export function getDurationFilterOptions(): FilterOption[] {
  return DURATION_NAMES.map((text, i) => ({
    text,
    value: String(i),
    styleClass: 'text-duration-' + i,
  }))
}

export function getLevelFilterOptions(): FilterOption[] {
  return Array.from({ length: 9 }, (_, i) => ({
    text: i + '★',
    value: String(i),
  }))
}

export function getMissionTypeFilterOptions(): FilterOption[] {
  return [
    { text: 'Home', value: '0' },
    { text: 'Virtue', value: '1' },
    { text: 'Unknown', value: '-1' },
  ]
}

export function getFarmFilterOptions(): FilterOption[] {
  return [
    { text: 'Home', value: '0' },
    { text: 'Virtue', value: '1' },
  ]
}

export function getBoolFilterOptions(): FilterOption[] {
  return [
    { text: 'True', value: 'true' },
    { text: 'False', value: 'false' },
  ]
}

export function getTargetFilterOptions(possibleTargets: PossibleTarget[]): FilterOption[] {
  return possibleTargets.map(t => ({
    text: t.displayName,
    value: String(t.id),
    imagePath: t.imageString,
  }))
}

export function getArtifactRarityFilterOptions(): FilterOption[] {
  return [
    { text: 'Common', value: '0' },
    { text: 'Rare', value: '1', styleClass: 'text-rare' },
    { text: 'Epic', value: '2', styleClass: 'text-epic' },
    { text: 'Legendary', value: '3', styleClass: 'text-legendary' },
  ]
}

export function getArtifactSpecTypeFilterOptions(): FilterOption[] {
  return [
    { text: 'Artifact', value: '0' },
    { text: 'Stone', value: '1' },
    { text: 'Stone Fragment', value: '2' },
    { text: 'Ingredient', value: '3' },
  ]
}

/**
 * Returns value options for a mission filter field. Handles ship, farm, duration, level, type,
 * dubcap, and buggedcap. Returns [] for target (caller must supply possibleTargets separately)
 * and drops (caller handles with its own modal).
 */
export function getMissionFilterValueOptions(topLevel: string): FilterOption[] {
  switch (topLevel) {
    case 'ship': return getShipFilterOptions()
    case 'farm': return getFarmFilterOptions()
    case 'duration': return getDurationFilterOptions()
    case 'level': return getLevelFilterOptions()
    case 'type': return getMissionTypeFilterOptions()
    case 'dubcap':
    case 'buggedcap': return getBoolFilterOptions()
    default: return []
  }
}

/**
 * Returns value options for a report artifact filter field. Returns [] for artifact_name
 * (caller handles with afxConfigs) and artifact_quality (free numeric input).
 */
export function getArtifactFilterValueOptions(topLevel: string): FilterOption[] {
  switch (topLevel) {
    case 'artifact_rarity': return getArtifactRarityFilterOptions()
    case 'artifact_spec_type': return getArtifactSpecTypeFilterOptions()
    default: return []
  }
}
