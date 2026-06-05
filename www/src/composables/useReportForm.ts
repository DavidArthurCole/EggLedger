import { reactive, watch, nextTick, type Ref } from 'vue'
import type { ReportDefinition, ReportFilters } from '../types/bridge'

export function makeBlankForm() {
  return {
    name: '',
    description: '',
    subject: 'ships',
    mode: 'aggregate',
    displayMode: 'bar',
    groupBy: 'ship_type',
    secondaryGroupBy: '',
    timeBucket: 'month',
    customBucketN: 3,
    customBucketUnit: 'month',
    gridW: 2,
    gridH: 2,
    color: '#6366f1',
    unfilledColor: '#1f2937',
    valueFilterOp: '',
    valueFilterThreshold: 0,
    normalizeBy: 'none',
    chartType: '',
    familyWeight: '',
    mennoEnabled: false,
    mennoCompareMode: 'side_by_side',
    minSampleSize: 0,
  }
}

export type ReportForm = ReturnType<typeof makeBlankForm>

interface ReportFormDeps {
  editingDef: Ref<ReportDefinition | null>
  builderMode: Ref<'basic' | 'advanced'>
  isDirty: Ref<boolean>
  closeWarning: Ref<boolean>
  shareWithAll: Ref<boolean>
  labelColorsMap: Ref<Record<string, string>>
  parseLabelColors: (raw: string) => Record<string, string>
  fromReportFilters: (filters: ReportFilters) => void
  clearFilters: () => void
}

/**
 * Owns the report builder form state and the editingDef -> form hydration. On an
 * editing def it populates the form, label colors, and filters; otherwise it resets
 * everything to a blank report. Initial hydration is deferred to the returned hydrate
 * call so the caller can finish wiring slice colors and filters first; the watch then
 * handles any later editingDef changes.
 */
export function useReportForm(deps: ReportFormDeps) {
  const form = reactive(makeBlankForm())

  function hydrate(def: ReportDefinition | null) {
    deps.builderMode.value = def ? 'advanced' : 'basic'
    deps.isDirty.value = false
    deps.closeWarning.value = false
    if (def) {
      form.name = def.name
      form.description = def.description || ''
      form.subject = def.subject === 'missions' ? 'ships' : def.subject
      form.mode = def.mode
      form.displayMode = def.displayMode
      form.groupBy = def.groupBy
      form.timeBucket = def.timeBucket || 'month'
      form.customBucketN = def.customBucketN || 3
      form.customBucketUnit = def.customBucketUnit || 'month'
      form.gridW = def.gridW || 2
      form.gridH = def.gridH || 2
      form.color = def.color || '#6366f1'
      form.unfilledColor = def.unfilledColor || '#1f2937'
      form.valueFilterOp = def.valueFilterOp || ''
      form.valueFilterThreshold = def.valueFilterThreshold || 0
      form.normalizeBy = def.normalizeBy || 'none'
      form.secondaryGroupBy = def.secondaryGroupBy || ''
      form.chartType = def.chartType || ''
      form.familyWeight = def.familyWeight || ''
      form.mennoEnabled = def.mennoEnabled ?? false
      form.mennoCompareMode = def.mennoCompareMode || 'side_by_side'
      form.minSampleSize = def.minSampleSize ?? 0
      deps.labelColorsMap.value = deps.parseLabelColors(def.labelColors)
      deps.fromReportFilters(def.filters)
    } else {
      Object.assign(form, makeBlankForm())
      deps.labelColorsMap.value = {}
      deps.clearFilters()
      deps.shareWithAll.value = false
    }
    nextTick(() => { deps.isDirty.value = false })
  }

  watch(() => deps.editingDef.value, hydrate)

  return { form, hydrate }
}
