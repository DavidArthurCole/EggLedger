import { ref, onMounted } from 'vue'
import type { ProgressSegment } from '../components/SegmentedProgressBar.vue'

/**
 * Storage backup/move state and handlers for the Settings view.
 *
 * Owns the storage location display, folder visibility toggle, the segmented
 * backup flow, and the move-and-restart flow. Loads the current storage path
 * and saved backup destination on mount.
 */
export function useStorageManagement() {
  const storagePath = ref('')
  const storageFolderHidden = ref(true)
  const backupDestPath = ref('')
  const backupInProgress = ref(false)
  const backupSuccess = ref(false)
  const backupError = ref('')
  const moveDestPath = ref('')
  const moveInProgress = ref(false)
  const moveError = ref('')

  const pendingSegment = (label: string): ProgressSegment => ({ label, status: 'pending', color: 'blue' })
  const activeSegment = (label: string): ProgressSegment => ({ label, status: 'active', color: 'blue', pulsing: true })
  const doneSegment = (label: string): ProgressSegment => ({ label, status: 'done', color: 'blue' })
  const failedSegment = (label: string): ProgressSegment => ({ label, status: 'failed', color: 'blue' })

  const backupDb = ref(true)
  const backupExports = ref(true)
  const backupLogs = ref(true)

  const backupSegments = ref<ProgressSegment[]>([
    pendingSegment('Database'),
    pendingSegment('Exports'),
    pendingSegment('Logs'),
  ])

  function openStorageFolder() {
    if (storagePath.value) {
      globalThis.openFileInFolder(storagePath.value)
    }
  }

  async function toggleStorageVisible() {
    const nowHidden = !storageFolderHidden.value
    storageFolderHidden.value = nowHidden
    await globalThis.setStorageFolderVisible(!nowHidden)
  }

  function onBackupDestChange() {
    globalThis.setBackupDestPath(backupDestPath.value)
  }

  async function chooseBackupDest() {
    const path = await globalThis.chooseFolderPath()
    if (path) {
      backupDestPath.value = path
      globalThis.setBackupDestPath(path)
    }
  }

  async function chooseMoveDest() {
    const path = await globalThis.chooseFolderPath()
    if (path) moveDestPath.value = path
  }

  async function runBackup() {
    backupSuccess.value = false
    backupError.value = ''
    backupInProgress.value = true
    const dest = backupDestPath.value
    const allParts: Array<{ part: 'internal' | 'exports' | 'logs'; label: string; enabled: boolean }> = [
      { part: 'internal', label: 'Database', enabled: backupDb.value },
      { part: 'exports', label: 'Exports', enabled: backupExports.value },
      { part: 'logs', label: 'Logs', enabled: backupLogs.value },
    ]
    const parts = allParts.filter(p => p.enabled)
    backupSegments.value = parts.map((p, i) => i === 0 ? activeSegment(p.label) : pendingSegment(p.label))
    try {
      for (let i = 0; i < parts.length; i++) {
        await globalThis.backupStoragePart(dest, parts[i].part)
        backupSegments.value[i] = doneSegment(parts[i].label)
        if (i + 1 < parts.length) {
          backupSegments.value[i + 1] = activeSegment(parts[i + 1].label)
        }
      }
      backupSuccess.value = true
    } catch (e: unknown) {
      backupError.value = e instanceof Error ? e.message : String(e)
      const activeIdx = backupSegments.value.findIndex(s => s.status === 'active')
      if (activeIdx >= 0) backupSegments.value[activeIdx] = failedSegment(parts[activeIdx].label)
    } finally {
      backupInProgress.value = false
    }
  }

  async function runMove() {
    moveError.value = ''
    moveInProgress.value = true
    try {
      await globalThis.moveStorageTo(moveDestPath.value)
    } catch (e: unknown) {
      moveError.value = e instanceof Error ? e.message : String(e)
      moveInProgress.value = false
    }
  }

  onMounted(async () => {
    storagePath.value = (await globalThis.getStoragePath()) ?? ''
    backupDestPath.value = (await globalThis.getBackupDestPath()) ?? ''
  })

  return {
    storagePath,
    storageFolderHidden,
    backupDestPath,
    backupInProgress,
    backupSuccess,
    backupError,
    moveDestPath,
    moveInProgress,
    moveError,
    backupDb,
    backupExports,
    backupLogs,
    backupSegments,
    openStorageFolder,
    toggleStorageVisible,
    onBackupDestChange,
    chooseBackupDest,
    chooseMoveDest,
    runBackup,
    runMove,
  }
}
