import { ref } from 'vue'
import type { ReportFilterCondition, ReportFilters } from '../types/bridge'

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

  function toReportFilters(): ReportFilters {
    return {
      and: andConditions.value.filter(c => c.topLevel !== '' && c.op !== ''),
      or: orGroups.value.map(g => g.filter(c => c.topLevel !== '' && c.op !== '')),
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
    toReportFilters,
    fromReportFilters,
    clearFilters,
  }
}
