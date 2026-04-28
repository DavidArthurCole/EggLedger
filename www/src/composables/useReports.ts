import { ref } from 'vue'
import type { BackfillStatus, ReportDefinition, ReportResult } from '../types/bridge'

const reports = ref<ReportDefinition[]>([])
const backfillStatus = ref<BackfillStatus>({ done: false, progress: 0 })

async function executeMennoComparison(
  id: string,
  rawRowLabels: string[],
  rawColLabels: string[],
): Promise<ReportResult | null> {
  const json = await globalThis.executeMennoComparison(
    id,
    JSON.stringify(rawRowLabels),
    JSON.stringify(rawColLabels),
  )
  if (!json || json === '{}') return null
  return JSON.parse(json) as ReportResult
}

export function useReports() {
  async function loadReports(accountId: string): Promise<void> {
    const json = await globalThis.getAccountReports(accountId)
    reports.value = JSON.parse(json) as ReportDefinition[]
  }

  async function createReport(def: ReportDefinition): Promise<ReportDefinition | null> {
    const json = await globalThis.createReport(JSON.stringify(def))
    if (!json) return null
    const saved = JSON.parse(json) as ReportDefinition
    reports.value = [...reports.value, saved]
    return saved
  }

  async function updateReport(def: ReportDefinition): Promise<boolean> {
    const json = await globalThis.updateReport(JSON.stringify(def))
    if (!json) return false
    const saved = JSON.parse(json) as ReportDefinition
    reports.value = reports.value.map(r => r.id === saved.id ? saved : r)
    return true
  }

  async function deleteReport(id: string): Promise<boolean> {
    const ok = await globalThis.deleteReport(id)
    if (ok) {
      reports.value = reports.value.filter(r => r.id !== id)
    }
    return ok
  }

  async function executeReport(id: string): Promise<ReportResult | null> {
    const json = await globalThis.executeReport(id)
    if (!json || json === '{}') return null
    return JSON.parse(json) as ReportResult
  }

  async function reorderReports(ids: string[]): Promise<boolean> {
    const ok = await globalThis.reorderReports(JSON.stringify(ids))
    if (ok) {
      const order = new Map(ids.map((id, i) => [id, i]))
      reports.value = [...reports.value].sort(
        (a, b) => (order.get(a.id) ?? 0) - (order.get(b.id) ?? 0),
      )
    }
    return ok
  }

  async function refreshBackfillStatus(): Promise<void> {
    const json = await globalThis.getReportBackfillStatus()
    backfillStatus.value = JSON.parse(json) as BackfillStatus
  }

  return {
    reports,
    backfillStatus,
    loadReports,
    createReport,
    updateReport,
    deleteReport,
    executeReport,
    executeMennoComparison,
    reorderReports,
    refreshBackfillStatus,
  }
}
