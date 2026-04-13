import type { MissionDrop, DatabaseMission } from './bridge'

export interface MennoConfigItem {
  artifactConfiguration: {
    artifactType: { id: number }
    artifactLevel: number
    artifactRarity: { id: number }
  }
  totalDrops: number
}

export interface ViewMissionDataMennoData {
  configs: MennoConfigItem[]
  totalDropsCount: number
}

export interface InnerDrop extends MissionDrop {
  count: number
  protoName?: string
  displayName?: string
}

export interface ViewMissionData {
  missionInfo: DatabaseMission & { targetInt?: number }
  artifacts: InnerDrop[]
  stones: InnerDrop[]
  stoneFragments: InnerDrop[]
  ingredients: InnerDrop[]
  launchDT: Date
  returnDT: Date
  durationStr: string
  capacityModifier: number | string
  prevMission: string | null
  nextMission: string | null
  missionCount?: number
  mennoData: ViewMissionDataMennoData
}
