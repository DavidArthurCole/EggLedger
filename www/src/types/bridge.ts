/**
 * bridge.ts - Typed definitions for all Go-to-JS bindings.
 * Mirrors every Go struct that appears in main.go UI bindings.
 * All lorca window bindings are declared here as globals so they are
 * accessible via globalThis and typed throughout the app.
 */

export interface Account {
  id: string
  nickname: string
  ebString: string
  accountColor: string
  seString: string
  peCount: number
  teCount: number
}

export interface DatabaseAccount {
  id: string
  nickname: string
  missionCount: number
  ebString: string
  accountColor: string
  /** Unix seconds; 0 if no missions exist */
  lastMissionReturnDT: number
}

export interface DatabaseMission {
  /** Unix seconds */
  launchDT: number
  /** Unix seconds */
  returnDT: number
  missionId: string
  /** ei.MissionInfo_Spaceship enum value */
  ship: number
  shipString: string
  /** ei.MissionInfo_DurationType enum value */
  durationType: number
  durationString: string
  level: number
  capacity: number
  nominalCapacity: number
  isDubCap: boolean
  isBuggedCap: boolean
  /** Proto name string e.g. "BOOK_OF_BASAN", or "" if none */
  target: string
  /** ei.ArtifactSpec_Name enum value, or -1 */
  targetInt: number
  /** ei.MissionInfo_MissionType enum value: 0=Home, 1=Virtue */
  missionType: number
  /** Display string e.g. "Home" or "Virtue" */
  missionTypeString: string
  /** Proto enum name e.g. "ATREGGIES" - used for ship icon path */
  shipEnumString: string
}

export interface MissionDrop {
  id: number
  specType: 'Artifact' | 'Stone' | 'StoneFragment' | 'Ingredient'
  name: string
  gameName: string
  effectString: string
  level: number
  rarity: number
  quality: number
  ivOrder: number
}

export interface PossibleTarget {
  displayName: string
  /** ei.ArtifactSpec_Name enum value, or -1 for "None (Pre 1.27)" */
  id: number
  imageString: string
}

export interface PossibleArtifact {
  /** ei.ArtifactSpec_Name enum value */
  name: number
  protoName: string
  displayName: string
  level: number
  rarity: number
  baseQuality: number
}

export interface DurationConfig {
  /** ei.MissionInfo_DurationType enum value */
  durationType: number
  minQuality: number
  maxQuality: number
  levelQualityBump: number
  maxLevels: number
}

export interface PossibleMission {
  /** ei.MissionInfo_Spaceship enum value */
  ship: number
  durations: DurationConfig[]
}

export interface IdNamePair {
  id: number
  name: string
}

export interface ShipConfiguration {
  shipType: IdNamePair
  shipDurationType: IdNamePair
  level: number
  targetArtifact: IdNamePair
}

export interface ArtifactConfiguration {
  artifactType: IdNamePair
  artifactRarity: IdNamePair
  artifactLevel: number
}

export interface ConfigurationItem {
  shipConfiguration: ShipConfiguration
  artifactConfiguration: ArtifactConfiguration
  totalDrops: number
}

export interface MissionProgress {
  total: number
  finished: number
  failed: number
  retried: number
  finishedPercentage: string
  expectedFinishTimestamp: number
  currentMission: string
}

export interface ProcessLogEntry {
  text: string
  isError: boolean
}

export interface SegmentStatus {
  name: string
  status: 'pending' | 'active' | 'done' | 'failed' | 'skipped'
}

export interface ProcessSnapshot {
  id: string
  label: string
  status: 'running' | 'done' | 'failed'
  /** Per-process log entries */
  logs: readonly ProcessLogEntry[]
  /** Unix milliseconds */
  startTimestamp: number
  /** "overall" | "mission" */
  kind: string
  segments: readonly SegmentStatus[]
}

export interface MennoDownloadProgress {
  /** "connecting" | "downloading" | "unzipping" | "saving" */
  phase: string
  /** bytes downloaded so far */
  bytesRead: number
  /** total bytes; -1 if Content-Length was not provided */
  totalBytes: number
  /** download speed in bytes per second */
  speedBps: number
  /** estimated seconds remaining; -1 if totalBytes is unknown */
  etaSeconds: number
}

export interface ReportFilterCondition {
  topLevel: string
  op: string
  val: string
}

export interface ReportFilters {
  and: ReportFilterCondition[]
  or: ReportFilterCondition[][]
}

export interface ReportDefinition {
  id: string
  accountId: string
  name: string
  subject: string
  mode: string
  displayMode: string
  groupBy: string
  timeBucket: string
  customBucketN: number
  customBucketUnit: string
  filters: ReportFilters
  gridX: number
  gridY: number
  gridW: number
  gridH: number
  weight: string
  color: string
  description: string
  chartType: string
  sortOrder: number
  createdAt: number
  updatedAt: number
  valueFilterOp: string
  valueFilterThreshold: number
  groupId: string
  normalizeBy: string
  /** JSON-encoded Record<string, string> mapping label to hex color for pie slices */
  labelColors: string
}

export interface ReportGroup {
  id: string
  accountId: string
  name: string
  sortOrder: number
  createdAt: number
}

export interface ReportResult {
  labels: string[]
  values: number[]
  floatValues: number[]
  isFloat: boolean
  weight: string
}

export interface BackfillStatus {
  done: boolean
  progress: number
}

export interface CloudSyncStatus {
  connected: boolean
  username: string
  avatarUrl: string
  /** Unix seconds; 0 if never pushed */
  lastPushAt: number
  /** Unix seconds; 0 if never pulled */
  lastPullAt: number
}

export enum AppState {
  AwaitingInput = 'AwaitingInput',
  FetchingSave = 'FetchingSave',
  ResolvingMissionTypes = 'ResolvingMissionTypes',
  FetchingMissions = 'FetchingMissions',
  ExportingData = 'ExportingData',
  Success = 'Success',
  Failed = 'Failed',
  Interrupted = 'Interrupted',
}

/**
 * Global declarations for all lorca-bound window functions.
 * Declared here so they are accessible via globalThis throughout the app.
 * These are bound by the Go binary at startup via ui.MustBind().
 */
declare global {
  // Settings
  function getDefaultScalingFactor(): Promise<number>
  function setDefaultScalingFactor(factor: number): Promise<void>
  function getDefaultResolution(): Promise<number[]>
  function setDefaultResolution(x: number, y: number): Promise<void>
  function setPreferredBrowser(path: string): Promise<boolean>
  function getPreferredBrowser(): Promise<string>
  function getLoadedBrowser(): Promise<string>
  function restartApp(): Promise<void>
  function getDetectedBrowsers(): Promise<string[]>
  function getAutoRefreshMennoPreference(): Promise<boolean>
  function setAutoRefreshMennoPreference(flag: boolean): Promise<void>
  function getStartInFullscreen(): Promise<boolean>
  function setStartInFullscreen(flag: boolean): Promise<void>
  function getDefaultViewMode(): Promise<string>
  function setDefaultViewMode(viewMode: string): Promise<void>
  function getAutoRetryPreference(): Promise<boolean>
  function setAutoRetryPreference(flag: boolean): Promise<void>
  function getHideTimeoutErrors(): Promise<boolean>
  function setHideTimeoutErrors(flag: boolean): Promise<void>
  function getScreenshotSafety(): Promise<boolean>
  function setScreenshotSafety(flag: boolean): Promise<void>
  function getShowMissionProgress(): Promise<boolean>
  function setShowMissionProgress(flag: boolean): Promise<void>
  function getCollapseOlderSections(): Promise<boolean>
  function setCollapseOlderSections(flag: boolean): Promise<void>
  function getAdvancedDropFilter(): Promise<boolean>
  function setAdvancedDropFilter(flag: boolean): Promise<void>
  function getMissionViewByDate(): Promise<boolean>
  function setMissionViewByDate(flag: boolean): Promise<void>
  function getMissionViewTimes(): Promise<boolean>
  function setMissionViewTimes(flag: boolean): Promise<void>
  function getMissionRecolorDC(): Promise<boolean>
  function setMissionRecolorDC(flag: boolean): Promise<void>
  function getMissionRecolorBC(): Promise<boolean>
  function setMissionRecolorBC(flag: boolean): Promise<void>
  function getMissionShowExpectedDrops(): Promise<boolean>
  function setMissionShowExpectedDrops(flag: boolean): Promise<void>
  function getMissionMultiViewMode(): Promise<string>
  function setMissionMultiViewMode(mode: string): Promise<void>
  function getMissionSortMethod(): Promise<string>
  function setMissionSortMethod(mode: string): Promise<void>
  function getLifetimeSortMethod(): Promise<string>
  function setLifetimeSortMethod(mode: string): Promise<void>
  function getLifetimeShowDropsPerShip(): Promise<boolean>
  function setLifetimeShowDropsPerShip(flag: boolean): Promise<void>
  function getLifetimeShowExpectedTotals(): Promise<boolean>
  function setLifetimeShowExpectedTotals(flag: boolean): Promise<void>
  function getWorkerCount(): Promise<number>
  function setWorkerCount(count: number): Promise<void>
  function filterWarningRead(): Promise<boolean>
  function setFilterWarningRead(flag: boolean): Promise<void>
  function workerCountWarningRead(): Promise<boolean>
  function setWorkerCountWarningRead(flag: boolean): Promise<void>

  // App info
  function appVersion(): Promise<string>
  function appDirectory(): Promise<string>
  function appIsInForbiddenDirectory(): Promise<boolean>
  function appIsTranslocated(): Promise<boolean>

  // Accounts
  function knownAccounts(): Promise<Account[]>
  function getExistingData(): Promise<DatabaseAccount[]>
  function addAccount(eid: string): Promise<Account>
  function getActiveAccountId(): Promise<string>
  function setActiveAccountId(id: string): Promise<void>

  // Missions
  function getMaxQuality(): Promise<number>
  function getAfxConfigs(): Promise<PossibleArtifact[]>
  function getPossibleTargets(): Promise<PossibleTarget[]>
  function getDurationConfigs(): Promise<PossibleMission[]>
  function getMissionIds(playerId: string): Promise<string[]>
  function viewMissionsOfEid(eid: string): Promise<DatabaseMission[]>
  function getMissionInfo(playerId: string, missionId: string): Promise<DatabaseMission>
  function getShipDrops(playerId: string, shipId: string): Promise<MissionDrop[]>
  function getAllPlayerDrops(playerId: string): Promise<Record<string, MissionDrop[]>>

  // Fetch pipeline
  function fetchPlayerData(playerId: string): Promise<void>
  function stopFetchingPlayerData(): Promise<void>

  // File ops
  function openFile(file: string): Promise<void>
  function openFileInFolder(file: string): Promise<void>
  function openURL(url: string): Promise<void>
  function chooseFolderPath(): Promise<string>

  // Storage management
  function getStoragePath(): Promise<string>
  function setStorageFolderVisible(visible: boolean): Promise<void>
  function backupStorageTo(destPath: string): Promise<void>
  function moveStorageTo(destPath: string): Promise<void>

  // Updates - returns [version, releaseNotes]
  function checkForUpdates(): Promise<[string, string]>

  // API version staleness
  function isApiVersionStale(): Promise<boolean>
  function getCompiledApiVersion(): Promise<string>

  // Menno data
  function isMennoRefreshNeeded(): Promise<boolean>
  function updateMennoData(): Promise<void>
  function secondsSinceLastMennoUpdate(): Promise<number>
  function loadMennoData(): Promise<boolean>
  function getMennoData(ship: number, shipDuration: number, shipLevel: number, targetArtifact: number): Promise<ConfigurationItem[]>

  // Reports
  function createReport(defJSON: string): Promise<string>
  function updateReport(defJSON: string): Promise<string>
  function deleteReport(id: string): Promise<boolean>
  function getAccountReports(accountId: string): Promise<string>
  function executeReport(id: string): Promise<string>
  function reorderReports(idsJSON: string): Promise<boolean>
  function getReportBackfillStatus(): Promise<string>
  function exportReport(id: string): Promise<string>
  function exportAllReports(accountId: string, destPath: string): Promise<string>
  function chooseSaveFilePath(defaultName: string): Promise<string>
  function importReport(accountId: string, jsonStr: string): Promise<string>
  function getAccountGroups(accountId: string): Promise<string>
  function createReportGroup(accountId: string, name: string): Promise<string>
  function renameReportGroup(id: string, name: string): Promise<boolean>
  function deleteReportGroup(id: string): Promise<boolean>
  function setReportGroup(reportId: string, groupId: string): Promise<boolean>
  function exportGroupReports(groupId: string): Promise<string>
  function importGroupReports(accountId: string, jsonStr: string): Promise<string>

  // Cloud sync
  function checkCloudReachable(): Promise<boolean>
  function getCloudSyncStatus(): Promise<string>
  function connectDiscord(): Promise<string>
  function disconnectCloud(): Promise<void>
  function syncToCloud(): Promise<void>
  function restoreFromCloud(): Promise<void>
  // Go-to-JS callbacks (assigned by Vue app, called by Go via lorca)
  var updateKnownAccounts: (accounts: Account[]) => void
  var updateState: (state: string) => void
  var updateMissionProgress: (progress: MissionProgress) => void
  var updateMennoDownloadProgress: (p: MennoDownloadProgress) => void
  var onMennoRefreshDone: (ok: boolean) => void
  var updateExportedFiles: (files: string[]) => void
  var emitMessage: (message: string, isError: boolean) => void
  var updateProcesses: (processes: ProcessSnapshot[]) => void
  var onDiscordAuthComplete: (connected: boolean, username: string) => void
  var onCloudSyncComplete: (success: boolean, errMsg: string) => void
  var onCloudRestoreComplete: (success: boolean, errMsg: string) => void
}
