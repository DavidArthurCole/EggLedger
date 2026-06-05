import { ref } from 'vue'
import type { ReportFilterCondition, ReportFilters } from '../types/bridge'
import { getReportField, defaultOpForField } from '../utils/filterFields'

export function useReportFilters() {
  const andConditions = ref<ReportFilterCondition[]>([])
  const orGroups = ref<ReportFilterCondition[][]>([])

  function addAndCondition() {
    andConditions.value = [...andConditions.value, { topLevel: '', op: '', val: '' }]
  }

  function removeAndCondition(index: number) {
    andConditions.value = andConditions.value.filter((_, i) => i !== index)
  }

  function updateAndCondition(index: number, patch: Partial<ReportFilterCondition>) {
    andConditions.value = andConditions.value.map((c, i) =>
      i === index ? { ...c, ...patch } : c,
    )
  }

  function addOrGroup() {
    orGroups.value = [...orGroups.value, [{ topLevel: '', op: '', val: '' }]]
  }

  function removeOrGroup(groupIndex: number) {
    orGroups.value = orGroups.value.filter((_, i) => i !== groupIndex)
  }

  function addToOrGroup(groupIndex: number) {
    orGroups.value = orGroups.value.map((g, i) =>
      i === groupIndex ? [...g, { topLevel: '', op: '', val: '' }] : g,
    )
  }

  function removeFromOrGroup(groupIndex: number, condIndex: number) {
    orGroups.value = orGroups.value.map((g, i) =>
      i === groupIndex ? g.filter((_, j) => j !== condIndex) : g,
    )
  }

  function updateOrCondition(
    groupIndex: number,
    condIndex: number,
    patch: Partial<ReportFilterCondition>,
  ) {
    orGroups.value = orGroups.value.map((g, i) =>
      i === groupIndex
        ? g.map((c, j) => (j === condIndex ? { ...c, ...patch } : c))
        : g,
    )
  }

  function fieldDefaults(newField: string): Partial<ReportFilterCondition> {
    const def = getReportField(newField)
    return {
      topLevel: newField,
      op: def ? defaultOpForField(def) : '',
      val: '',
    }
  }

  /**
   * Replaces an AND condition's field, resetting op to the field's default and clearing val so a
   * stale value from a different field type can never survive.
   */
  function setField(index: number, newField: string) {
    updateAndCondition(index, fieldDefaults(newField))
  }

  /**
   * Replaces an OR condition's field, resetting op to the field's default and clearing val so a
   * stale value from a different field type can never survive.
   */
  function setOrField(groupIndex: number, condIndex: number, newField: string) {
    updateOrCondition(groupIndex, condIndex, fieldDefaults(newField))
  }

  /**
   * Removes any AND or OR condition whose field scope is not in validScopes. Conditions with an
   * empty field are kept. OR groups left empty after pruning are dropped.
   */
  function pruneConditions(validScopes: Set<'mission' | 'artifact'>) {
    const inScope = (c: ReportFilterCondition) => {
      if (c.topLevel === '') return true
      const def = getReportField(c.topLevel)
      return def ? validScopes.has(def.scope) : true
    }
    andConditions.value = andConditions.value.filter(inScope)
    orGroups.value = orGroups.value
      .map(g => g.filter(inScope))
      .filter(g => g.length > 0)
  }

  function toReportFilters(): ReportFilters {
    const keep = (c: ReportFilterCondition) =>
      c.topLevel !== '' && c.op !== '' && !(c.topLevel === 'drops' && c.val === '')
    return {
      and: andConditions.value.filter(keep),
      or: orGroups.value.map(g => g.filter(keep)),
    }
  }

  function fromReportFilters(filters: ReportFilters) {
    andConditions.value = filters.and.map(c => ({ ...c }))
    orGroups.value = filters.or.map(g => g.map(c => ({ ...c })))
  }

  function clearFilters() {
    andConditions.value = []
    orGroups.value = []
  }

  return {
    andConditions,
    orGroups,
    addAndCondition,
    removeAndCondition,
    updateAndCondition,
    addOrGroup,
    removeOrGroup,
    addToOrGroup,
    removeFromOrGroup,
    updateOrCondition,
    setField,
    setOrField,
    pruneConditions,
    toReportFilters,
    fromReportFilters,
    clearFilters,
  }
}
