import { ref } from 'vue'
import type { ReportGroup } from '../types/bridge'

const groups = ref<ReportGroup[]>([])

export function useReportGroups() {
  async function loadGroups(accountId: string): Promise<void> {
    const json = await globalThis.getAccountGroups(accountId)
    groups.value = JSON.parse(json) as ReportGroup[]
  }

  async function createGroup(accountId: string, name: string): Promise<string | null> {
    const id = await globalThis.createReportGroup(accountId, name)
    if (!id) return null
    await loadGroups(accountId)
    return id
  }

  async function renameGroup(accountId: string, id: string, name: string): Promise<boolean> {
    const ok = await globalThis.renameReportGroup(id, name)
    if (ok) await loadGroups(accountId)
    return ok
  }

  async function deleteGroup(accountId: string, id: string): Promise<boolean> {
    const ok = await globalThis.deleteReportGroup(id)
    if (ok) await loadGroups(accountId)
    return ok
  }

  return { groups, loadGroups, createGroup, renameGroup, deleteGroup }
}
