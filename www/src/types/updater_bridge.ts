/**
 * updater_bridge.ts - Type declarations for the in-place updater bindings.
 */
declare global {
  function downloadAndInstallUpdate(tag: string): Promise<void>
  var updateDownloadProgress: (downloaded: number, total: number) => void
}

export {}
