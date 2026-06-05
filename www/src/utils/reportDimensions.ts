/**
 * Canonical report group-by dimension metadata. Single source of truth for the dimension
 * value/label pairs used by both the advanced (ReportBuilderPanel) and guided
 * (ReportBuilderGuided) report builders. The `value` strings are wire/SQL keys and must never
 * change; labels are display-only and use Title Case to match the advanced builder.
 */
export interface ReportDimension {
  /** Wire/SQL key for the dimension. Never change this. */
  value: string
  /** Title Case display label. */
  label: string
  /** Whether this dimension groups missions or artifact drops. */
  scope: 'mission' | 'artifact'
}

/** Mission-scoped group-by dimensions. */
export const MISSION_DIMENSIONS: ReportDimension[] = [
  { value: 'ship_type', label: 'Ship Type', scope: 'mission' },
  { value: 'duration_type', label: 'Duration Type', scope: 'mission' },
  { value: 'level', label: 'Level', scope: 'mission' },
  { value: 'mission_type', label: 'Mission Type', scope: 'mission' },
  { value: 'mission_target', label: 'Mission Target', scope: 'mission' },
]

/** Artifact-scoped group-by dimensions. */
export const ARTIFACT_DIMENSIONS: ReportDimension[] = [
  { value: 'artifact_name', label: 'Artifact Name', scope: 'artifact' },
  { value: 'rarity', label: 'Rarity', scope: 'artifact' },
  { value: 'tier', label: 'Tier', scope: 'artifact' },
  { value: 'spec_type', label: 'Spec Type', scope: 'artifact' },
]

/** All group-by dimensions, mission dimensions first. */
export const ALL_DIMENSIONS: ReportDimension[] = [...MISSION_DIMENSIONS, ...ARTIFACT_DIMENSIONS]

/** Set of artifact dimension value keys, for scope checks. */
export const ARTIFACT_DIMENSION_KEYS: Set<string> = new Set(ARTIFACT_DIMENSIONS.map(d => d.value))

/** Returns the Title Case label for a dimension value, falling back to the value itself. */
export function dimensionLabel(value: string): string {
  return ALL_DIMENSIONS.find(d => d.value === value)?.label ?? value
}
