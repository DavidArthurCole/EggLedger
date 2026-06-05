import { ref, computed, watch } from 'vue'
import type { ProgressSegment } from '../components/SegmentedProgressBar.vue'

export enum LifetimeLoadState {
  Idle = 'Idle',
  FetchingIds = 'FetchingIds',
  FilteringIds = 'FilteringIds',
  LoadingMissionData = 'LoadingMissionData',
  Success = 'Success',
  Failed = 'Failed',
  FailedTooFast = 'FailedTooFast',
}

export interface LifetimeDataLoadedProgress {
  percentageDone: string
  loadedCount: number
  totalCount: number
}

type LifetimeSegmentStatus = 'pending' | 'active' | 'done' | 'failed' | 'skipped'

function resolveLifetimeSegments(
  s: LifetimeLoadState,
  hadFilter: boolean,
  lastPhase: LifetimeLoadState,
): { seg1: LifetimeSegmentStatus, seg2: LifetimeSegmentStatus, seg3: LifetimeSegmentStatus } {
  const seg2Done: LifetimeSegmentStatus = hadFilter ? 'done' : 'skipped'
  const failed = s === LifetimeLoadState.Failed || s === LifetimeLoadState.FailedTooFast
  if (s === LifetimeLoadState.FetchingIds) return { seg1: 'active', seg2: 'pending', seg3: 'pending' }
  if (s === LifetimeLoadState.FilteringIds) return { seg1: 'done', seg2: 'active', seg3: 'pending' }
  if (s === LifetimeLoadState.LoadingMissionData) return { seg1: 'done', seg2: seg2Done, seg3: 'active' }
  if (s === LifetimeLoadState.Success) return { seg1: 'done', seg2: seg2Done, seg3: 'done' }
  if (failed) {
    if (lastPhase === LifetimeLoadState.LoadingMissionData) return { seg1: 'done', seg2: seg2Done, seg3: 'failed' }
    if (lastPhase === LifetimeLoadState.FilteringIds) return { seg1: 'done', seg2: 'failed', seg3: 'pending' }
    return { seg1: 'failed', seg2: 'pending', seg3: 'pending' }
  }
  return { seg1: 'pending', seg2: 'pending', seg3: 'pending' }
}

/**
 * Progress and segment-bar machinery for the Lifetime Data tab. Owns the load
 * state and progress refs (which the caller's loader mutates) plus all derived
 * computeds that drive the SegmentedProgressBar.
 */
export function useLifetimeProgress() {
  const lifetimeState = ref<LifetimeLoadState>(LifetimeLoadState.Idle)
  const lifetimeSuccessTime = ref<string | null>(null)
  const lifetimeDataBeingFiltered = ref(false)
  const lifetimeDataExcludedCount = ref(0)
  const lifetimeDataLoadedProgress = ref<LifetimeDataLoadedProgress>({
    percentageDone: '0%',
    loadedCount: 0,
    totalCount: 0,
  })

  const lifetimeDataBeingLoaded = computed(
    () =>
      lifetimeState.value === LifetimeLoadState.FetchingIds ||
      lifetimeState.value === LifetimeLoadState.LoadingMissionData,
  )

  const isProgressSpinning = computed(
    () =>
      lifetimeState.value === LifetimeLoadState.FetchingIds ||
      lifetimeState.value === LifetimeLoadState.FilteringIds ||
      lifetimeState.value === LifetimeLoadState.LoadingMissionData,
  )

  const hadFilteringPhase = ref(false)
  const lastActivePhase = ref<LifetimeLoadState>(LifetimeLoadState.Idle)

  watch(lifetimeState, (s) => {
    if (s === LifetimeLoadState.FetchingIds) {
      hadFilteringPhase.value = false
      lastActivePhase.value = LifetimeLoadState.Idle
    }
    if (s === LifetimeLoadState.FilteringIds) hadFilteringPhase.value = true
    if (
      s !== LifetimeLoadState.Failed &&
      s !== LifetimeLoadState.FailedTooFast &&
      s !== LifetimeLoadState.Success &&
      s !== LifetimeLoadState.Idle
    ) {
      lastActivePhase.value = s
    }
  })

  const lifetimeSegmentStates = computed(() => {
    const s = lifetimeState.value
    const p = lifetimeDataLoadedProgress.value
    const { seg1, seg2, seg3 } = resolveLifetimeSegments(s, hadFilteringPhase.value, lastActivePhase.value)

    let filterPct = 0
    if (s === LifetimeLoadState.FilteringIds && p.totalCount > 0) {
      filterPct = Math.round((p.loadedCount / p.totalCount) * 100)
    } else if (seg2 === 'done') {
      filterPct = 100
    }
    const filterPulsing = seg2 === 'active' && filterPct === 0
    const visibleFilterPct = seg2 === 'active' ? Math.max(filterPct, 3) : filterPct

    let loadPct = 0
    if (s === LifetimeLoadState.LoadingMissionData && p.totalCount > 0) {
      loadPct = Math.round((p.loadedCount / p.totalCount) * 100)
    } else if (seg3 === 'done') {
      loadPct = 100
    }
    const loadPulsing = seg3 === 'active' && loadPct === 0
    const visibleLoadPct = seg3 === 'active' ? Math.max(loadPct, 3) : loadPct

    return { seg1, seg2, seg3, filterPct: visibleFilterPct, filterPulsing, loadPct: visibleLoadPct, loadPulsing }
  })

  const lifetimeProgressSegments = computed((): ProgressSegment[] => {
    const { seg1, seg2, seg3, filterPct, filterPulsing, loadPct, loadPulsing } = lifetimeSegmentStates.value
    return [
      { label: 'IDs', status: seg1, color: 'blue', pulsing: seg1 === 'active' },
      { label: 'Filter', status: seg2, color: 'violet', widthPct: filterPct, pulsing: filterPulsing },
      { label: 'Load', status: seg3, color: 'green', widthPct: loadPct, pulsing: loadPulsing },
    ]
  })

  const statusColor = computed(() => {
    switch (lifetimeState.value) {
      case LifetimeLoadState.Success:
        return 'text-green-500'
      case LifetimeLoadState.Failed:
      case LifetimeLoadState.FailedTooFast:
        return 'text-red-700'
      default:
        return 'text-gray-300'
    }
  })

  const statusText = computed(() => {
    switch (lifetimeState.value) {
      case LifetimeLoadState.FetchingIds:
        return 'Fetching mission IDs...'
      case LifetimeLoadState.FilteringIds:
        return `Filtering mission IDs... ${lifetimeDataLoadedProgress.value.loadedCount}/${lifetimeDataLoadedProgress.value.totalCount}`
      case LifetimeLoadState.LoadingMissionData:
        return `Loading missions... ${lifetimeDataLoadedProgress.value.loadedCount}/${lifetimeDataLoadedProgress.value.totalCount}`
      case LifetimeLoadState.Success:
        return 'Succeeded' + (lifetimeSuccessTime.value == null ? '' : ` in ${lifetimeSuccessTime.value}`)
      case LifetimeLoadState.Failed:
        return 'Failed'
      case LifetimeLoadState.FailedTooFast:
        return 'Failed: Data loaded too quickly after start. Try again.'
      default:
        return ''
    }
  })

  return {
    lifetimeState,
    lifetimeSuccessTime,
    lifetimeDataBeingFiltered,
    lifetimeDataExcludedCount,
    lifetimeDataLoadedProgress,
    lifetimeDataBeingLoaded,
    isProgressSpinning,
    lifetimeProgressSegments,
    statusColor,
    statusText,
  }
}
