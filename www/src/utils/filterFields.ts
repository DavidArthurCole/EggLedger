import type { FilterOption } from './filterOptions'
import type { PossibleTarget, PossibleArtifact } from '../types/bridge'
import {
  getMissionFilterValueOptions,
  getTargetFilterOptions,
  getDropFilterOptions,
  getArtifactRarityFilterOptions,
  getArtifactSpecTypeFilterOptions,
  getArtifactTierFilterOptions,
  getArtifactNameFilterOptions,
} from './filterOptions'

export interface FilterFieldCtx {
  possibleTargets: PossibleTarget[]
  artifactConfigs: PossibleArtifact[]
  maxQuality: number
}

export type FilterValueKind = 'select' | 'modal' | 'date' | 'bool' | 'number'

export interface FilterOp {
  value: string
  label: string
}

export interface FilterFieldDef {
  key: string
  label: string
  scope: 'mission' | 'artifact'
  valueKind: FilterValueKind
  ops: FilterOp[]
  /** For 'select' and 'modal' kinds: builds the option list. */
  optionsSource?: (ctx: FilterFieldCtx) => FilterOption[]
}

const EQUALITY_OPS: FilterOp[] = [
  { value: '=', label: 'is' },
  { value: '!=', label: 'is not' },
]

const COMPARISON_OPS: FilterOp[] = [
  { value: '=', label: 'is' },
  { value: '!=', label: 'is not' },
  { value: '>', label: 'greater than' },
  { value: '<', label: 'less than' },
  { value: '>=', label: 'at least' },
  { value: '<=', label: 'at most' },
]

const DATE_OPS: FilterOp[] = [
  { value: '=', label: 'on' },
  { value: '<', label: 'before' },
  { value: '>', label: 'after' },
  { value: '<=', label: 'on or before' },
  { value: '>=', label: 'on or after' },
]

const DROPS_OPS: FilterOp[] = [
  { value: 'c', label: 'contains' },
  { value: 'dnc', label: 'does not contain' },
]

const BOOL_OPS: FilterOp[] = [
  { value: 'true', label: 'True' },
  { value: 'false', label: 'False' },
]

export const REPORT_FILTER_FIELDS: FilterFieldDef[] = [
  {
    key: 'ship',
    label: 'Ship',
    scope: 'mission',
    valueKind: 'select',
    ops: COMPARISON_OPS,
    optionsSource: () => getMissionFilterValueOptions('ship'),
  },
  {
    key: 'duration',
    label: 'Duration',
    scope: 'mission',
    valueKind: 'select',
    ops: COMPARISON_OPS,
    optionsSource: () => getMissionFilterValueOptions('duration'),
  },
  {
    key: 'level',
    label: 'Level',
    scope: 'mission',
    valueKind: 'select',
    ops: COMPARISON_OPS,
    optionsSource: () => getMissionFilterValueOptions('level'),
  },
  {
    key: 'target',
    label: 'Target',
    scope: 'mission',
    valueKind: 'modal',
    ops: EQUALITY_OPS,
    optionsSource: ctx => getTargetFilterOptions(ctx.possibleTargets),
  },
  {
    key: 'type',
    label: 'Mission Type',
    scope: 'mission',
    valueKind: 'select',
    ops: EQUALITY_OPS,
    optionsSource: () => getMissionFilterValueOptions('type'),
  },
  {
    key: 'launchDT',
    label: 'Launch Date',
    scope: 'mission',
    valueKind: 'date',
    ops: DATE_OPS,
  },
  {
    key: 'returnDT',
    label: 'Return Date',
    scope: 'mission',
    valueKind: 'date',
    ops: DATE_OPS,
  },
  {
    key: 'dubcap',
    label: 'Dub cap',
    scope: 'mission',
    valueKind: 'bool',
    ops: BOOL_OPS,
  },
  {
    key: 'buggedcap',
    label: 'Bugged cap',
    scope: 'mission',
    valueKind: 'bool',
    ops: BOOL_OPS,
  },
  {
    key: 'drops',
    label: 'Drops',
    scope: 'mission',
    valueKind: 'modal',
    ops: DROPS_OPS,
    optionsSource: ctx => getDropFilterOptions(ctx.artifactConfigs, ctx.maxQuality, true),
  },
  {
    key: 'artifact_name',
    label: 'Name',
    scope: 'artifact',
    valueKind: 'modal',
    ops: EQUALITY_OPS,
    optionsSource: ctx => getArtifactNameFilterOptions(ctx.artifactConfigs),
  },
  {
    key: 'artifact_rarity',
    label: 'Rarity',
    scope: 'artifact',
    valueKind: 'select',
    ops: COMPARISON_OPS,
    optionsSource: () => getArtifactRarityFilterOptions(),
  },
  {
    key: 'artifact_tier',
    label: 'Tier',
    scope: 'artifact',
    valueKind: 'select',
    ops: COMPARISON_OPS,
    optionsSource: ctx => getArtifactTierFilterOptions(ctx.artifactConfigs),
  },
  {
    key: 'artifact_spec_type',
    label: 'Spec Type',
    scope: 'artifact',
    valueKind: 'select',
    ops: EQUALITY_OPS,
    optionsSource: () => getArtifactSpecTypeFilterOptions(),
  },
  {
    key: 'artifact_quality',
    label: 'Quality',
    scope: 'artifact',
    valueKind: 'number',
    ops: COMPARISON_OPS,
  },
]

/** Returns the field definition for a given key, or undefined if no field matches. */
export function getReportField(key: string): FilterFieldDef | undefined {
  return REPORT_FILTER_FIELDS.find(f => f.key === key)
}

/**
 * Returns the default operator value for a field. Bool fields default to 'true' and drops defaults
 * to 'c'; every other field uses its first operator.
 */
export function defaultOpForField(def: FilterFieldDef): string {
  if (def.valueKind === 'bool') return 'true'
  if (def.key === 'drops') return 'c'
  return def.ops[0]?.value ?? ''
}

/** Returns the report filter fields scoped to missions. */
export function reportMissionFields(): FilterFieldDef[] {
  return REPORT_FILTER_FIELDS.filter(f => f.scope === 'mission')
}

/** Returns the report filter fields scoped to artifacts. */
export function reportArtifactFields(): FilterFieldDef[] {
  return REPORT_FILTER_FIELDS.filter(f => f.scope === 'artifact')
}
