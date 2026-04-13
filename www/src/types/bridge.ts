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

  // Missions
  function getMaxQuality(): Promise<number>
  function getAfxConfigs(): Promise<PossibleArtifact[]>
  function getPossibleTargets(): Promise<PossibleTarget[]>
  function getDurationConfigs(): Promise<PossibleMission[]>
  function getMissionIds(playerId: string): Promise<string[]>
  function viewMissionsOfEid(eid: string): Promise<DatabaseMission[]>
  function getMissionInfo(playerId: string, missionId: string): Promise<DatabaseMission>
  function getShipDrops(playerId: string, shipId: string): Promise<MissionDrop[]>

  // Fetch pipeline
  function fetchPlayerData(playerId: string): Promise<void>
  function stopFetchingPlayerData(): Promise<void>

  // File ops
  function openFile(file: string): Promise<void>
  function openFileInFolder(file: string): Promise<void>
  function openURL(url: string): Promise<void>

  // Updates - returns [version, releaseNotes]
  function checkForUpdates(): Promise<[string, string]>

  // Menno data
  function isMennoRefreshNeeded(): Promise<boolean>
  function updateMennoData(): Promise<void>
  function secondsSinceLastMennoUpdate(): Promise<number>
  function loadMennoData(): Promise<boolean>
  function getMennoData(ship: number, shipDuration: number, shipLevel: number, targetArtifact: number): Promise<ConfigurationItem[]>

  // Go-to-JS callbacks (assigned by Vue app, called by Go via lorca)
  var updateKnownAccounts: (accounts: Account[]) => void
  var updateState: (state: string) => void
  var updateMissionProgress: (progress: MissionProgress) => void
  var updateMennoDownloadProgress: (p: MennoDownloadProgress) => void
  var onMennoRefreshDone: (ok: boolean) => void
  var updateExportedFiles: (files: string[]) => void
  var emitMessage: (message: string, isError: boolean) => void
  var updateProcesses: (processes: ProcessSnapshot[]) => void
}
