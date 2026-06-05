import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import type { Ref, WatchSource } from 'vue'

type CloudStatusPayload = { connected: boolean; username: string; avatarUrl: string; lastPushAt: number; lastPullAt: number; hasEncryptionKey: boolean }

function clearFlash(flashRef: Ref<boolean>) {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => { flashRef.value = false })
  })
}

function triggerFlash(flashRef: Ref<boolean>) {
  flashRef.value = true
  nextTick(() => clearFlash(flashRef))
}

/**
 * Cloud-sync (Discord) state and handlers for the Settings view.
 *
 * Owns all `cloud*` reactive state, the reachability check, status polling,
 * Discord auth handlers, and the sync/restore/delete flows. Initializes itself
 * on mount and clears its lorca callbacks on unmount.
 *
 * @param autoSyncWatchSources - Settings refs whose changes should trigger an
 *   automatic sync when auto-sync is enabled and the cloud is connected.
 */
export function useCloudSync(autoSyncWatchSources: WatchSource[] = []) {
  const cloudReachableChecking = ref(false)
  const cloudReachable = ref(false)
  const cloudConnected = ref(false)
  const cloudUsername = ref('')
  const cloudAvatarUrl = ref('')
  const cloudHasEncryptionKey = ref(false)
  const cloudLastPushAt = ref(0)
  const cloudLastPullAt = ref(0)
  const cloudAuthWaiting = ref(false)
  const cloudAuthURL = ref('')
  const cloudSyncInProgress = ref(false)
  const cloudSyncError = ref('')
  const cloudRestoreInProgress = ref(false)
  const cloudRestoreError = ref('')
  const cloudRestoreSuccess = ref(false)
  const confirmingDeleteRemote = ref(false)
  const deleteRemoteInProgress = ref(false)
  const deleteRemoteError = ref('')
  const cloudAutoSync = ref(false)

  const cloudLastPushString = computed(() => {
    if (!cloudLastPushAt.value) return ''
    return new Date(cloudLastPushAt.value * 1000).toLocaleString()
  })

  const cloudLastPullString = computed(() => {
    if (!cloudLastPullAt.value) return ''
    return new Date(cloudLastPullAt.value * 1000).toLocaleString()
  })

  const sessionExpiryString = computed(() => {
    const lastActivity = Math.max(cloudLastPushAt.value, cloudLastPullAt.value)
    if (!lastActivity) return ''
    const expiryMs = (lastActivity + 30 * 24 * 3600) * 1000
    return new Date(expiryMs).toLocaleString()
  })

  const pushFlashing = ref(false)
  const pullFlashing = ref(false)
  const sessionFlashing = ref(false)

  watch(cloudLastPushAt, () => {
    triggerFlash(pushFlashing)
    triggerFlash(sessionFlashing)
  })
  watch(cloudLastPullAt, () => {
    triggerFlash(pullFlashing)
    triggerFlash(sessionFlashing)
  })

  function applyCloudStatus(status: CloudStatusPayload) {
    cloudConnected.value = status.connected
    cloudUsername.value = status.username ?? ''
    cloudAvatarUrl.value = status.avatarUrl ?? ''
    cloudLastPushAt.value = status.lastPushAt ?? 0
    cloudLastPullAt.value = status.lastPullAt ?? 0
    cloudHasEncryptionKey.value = status.hasEncryptionKey ?? false
  }

  async function initCloudSync() {
    applyCloudStatus(JSON.parse(await globalThis.getCloudSyncStatus()))

    cloudReachableChecking.value = true
    cloudReachable.value = await globalThis.checkCloudReachable()
    cloudReachableChecking.value = false
    if (!cloudReachable.value) return

    globalThis.onDiscordAuthComplete = (connected: boolean, username: string) => {
      cloudAuthWaiting.value = false
      cloudConnected.value = connected
      cloudUsername.value = username ?? ''
      if (connected) {
        cloudSyncError.value = ''
        cloudRestoreError.value = ''
        refreshSyncStatus()
      }
    }
    globalThis.onCloudSyncComplete = (success: boolean, errMsg: string) => {
      cloudSyncInProgress.value = false
      if (success) {
        cloudSyncError.value = ''
        refreshSyncStatus()
      } else {
        cloudSyncError.value = errMsg
      }
    }
    globalThis.onCloudRestoreComplete = (success: boolean, errMsg: string) => {
      cloudRestoreInProgress.value = false
      if (success) {
        cloudRestoreSuccess.value = true
        cloudRestoreError.value = ''
      } else {
        cloudRestoreError.value = errMsg
      }
    }
  }

  async function recheckReachable() {
    cloudReachableChecking.value = true
    cloudReachable.value = await globalThis.checkCloudReachable()
    cloudReachableChecking.value = false
    if (cloudReachable.value) {
      await initCloudSync()
    }
  }

  async function refreshSyncStatus() {
    applyCloudStatus(JSON.parse(await globalThis.getCloudSyncStatus()))
  }

  async function connectDiscord() {
    cloudAuthWaiting.value = true
    cloudAuthURL.value = ''
    try {
      const url = await globalThis.connectDiscord()
      cloudAuthURL.value = url ?? ''
    } catch (_e: unknown) {
      console.error('Error initiating Discord connection:', _e)
      cloudAuthWaiting.value = false
    }
  }

  function openAuthURL() {
    if (cloudAuthURL.value) {
      globalThis.openURL(cloudAuthURL.value)
    }
  }

  async function disconnectCloud() {
    await globalThis.disconnectCloud()
    cloudConnected.value = false
    cloudUsername.value = ''
    cloudAvatarUrl.value = ''
    cloudLastPushAt.value = 0
    cloudLastPullAt.value = 0
  }

  function syncNow() {
    cloudSyncInProgress.value = true
    cloudSyncError.value = ''
    globalThis.syncToCloud()
  }

  function restoreNow() {
    cloudRestoreInProgress.value = true
    cloudRestoreError.value = ''
    cloudRestoreSuccess.value = false
    globalThis.restoreFromCloud()
  }

  async function deleteRemoteData() {
    deleteRemoteInProgress.value = true
    deleteRemoteError.value = ''
    confirmingDeleteRemote.value = false
    const errMsg = await globalThis.deleteRemoteData()
    deleteRemoteInProgress.value = false
    if (errMsg) {
      deleteRemoteError.value = errMsg
    }
  }

  async function onAutoSyncChange() {
    await globalThis.setCloudAutoSync(cloudAutoSync.value)
  }

  if (autoSyncWatchSources.length > 0) {
    watch(
      autoSyncWatchSources,
      () => {
        if (cloudAutoSync.value && cloudConnected.value && !cloudSyncInProgress.value) {
          syncNow()
        }
      },
    )
  }

  onMounted(async () => {
    cloudAutoSync.value = (await globalThis.getCloudAutoSync()) ?? false
    await initCloudSync()
  })

  onUnmounted(() => {
    globalThis.onDiscordAuthComplete = () => {}
    globalThis.onCloudSyncComplete = () => {}
    globalThis.onCloudRestoreComplete = () => {}
  })

  return {
    cloudReachableChecking,
    cloudReachable,
    cloudConnected,
    cloudUsername,
    cloudAvatarUrl,
    cloudHasEncryptionKey,
    cloudLastPushAt,
    cloudLastPullAt,
    cloudAuthWaiting,
    cloudAuthURL,
    cloudSyncInProgress,
    cloudSyncError,
    cloudRestoreInProgress,
    cloudRestoreError,
    cloudRestoreSuccess,
    confirmingDeleteRemote,
    deleteRemoteInProgress,
    deleteRemoteError,
    cloudAutoSync,
    cloudLastPushString,
    cloudLastPullString,
    sessionExpiryString,
    pushFlashing,
    pullFlashing,
    sessionFlashing,
    recheckReachable,
    connectDiscord,
    openAuthURL,
    disconnectCloud,
    syncNow,
    restoreNow,
    deleteRemoteData,
    onAutoSyncChange,
  }
}
