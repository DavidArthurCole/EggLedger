import type { PossibleTarget, PossibleArtifact } from '../types/bridge'

export interface FilterOption {
  text: string
  value: string
  styleClass?: string
  imagePath?: string
  rarity?: number
  rarityGif?: string
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

// eslint-disable-next-line sonarjs/cognitive-complexity
export function getDropFilterOptions(
  artifactConfigs: PossibleArtifact[],
  maxQuality: number,
  advanced: boolean,
): FilterOption[] {
  const artifactList = artifactConfigs.filter((a) => a.baseQuality <= maxQuality)

  const result: FilterOption[] = [
    { text: 'Any Rare', value: '%_%_1_%', rarity: 1, styleClass: 'text-rare', imagePath: 'icon_help.webp' },
    { text: 'Any Epic', value: '%_%_2_%', rarity: 2, styleClass: 'text-epic', imagePath: 'icon_help.webp' },
    { text: 'Any Legendary', value: '%_%_3_%', rarity: 3, styleClass: 'text-legendary', imagePath: 'icon_help.webp' },
  ]

  const byFamily = new Map<number, Map<number, PossibleArtifact[]>>()
  for (const a of artifactList) {
    if (!byFamily.has(a.name)) byFamily.set(a.name, new Map())
    const byTier = byFamily.get(a.name)!
    if (!byTier.has(a.level)) byTier.set(a.level, [])
    byTier.get(a.level)!.push(a)
  }

  for (const [familyId, tierMap] of byFamily) {
    if (advanced) {
      const firstArtifact = [...tierMap.values()][0][0]
      result.push({
        text: firstArtifact.displayName + ' (Any)',
        value: familyId + '_%_%_%',
        rarity: 0,
        imagePath: dropPath(firstArtifact),
      })
    }
    for (const [tierLevel, rarities] of tierMap) {
      if (advanced && rarities.some((a) => a.rarity > 0)) {
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
