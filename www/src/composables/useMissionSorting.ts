export interface DropLike {
  id: number
  name: string
  level: number
  rarity: number
  quality: number
  ivOrder: number
  specType: string
  count?: number
}

export function groupedSpecType(collection: DropLike[]): Record<string, DropLike> {
  return collection.reduce((acc, obj) => {
    const key = obj.name + '_' + obj.level + '_' + obj.specType + '_' + obj.rarity
    if (acc[key]) {
      (acc[key].count as number)++
    } else {
      acc[key] = obj
      acc[key].count = 1
    }
    return acc
  }, {} as Record<string, DropLike>)
}

export function sortGroupAlreadyCombed<T extends DropLike>(collection: T[]): T[] {
  return collection
    .sort((a, b) => {
      if (a.level > b.level) return -1
      if (a.level < b.level) return 1
      if (a.rarity > b.rarity) return -1
      if (a.rarity < b.rarity) return 1
      if (a.id > b.id) return -1
      if (a.id < b.id) return 1
      if (a.quality < b.quality) return -1
      if (a.quality > b.quality) return 1
      return 0
    })
    .reverse()
}

export function sortedGroupedSpecType<T extends DropLike>(collection: T[]): T[] {
  return sortGroupAlreadyCombed(Object.values(groupedSpecType(collection as unknown as DropLike[])) as T[])
}

export function inventoryVisualizerSort<T extends DropLike>(collection: T[]): T[] {
  return collection.sort((a, b) => {
    if (a.rarity > b.rarity) return -1
    if (a.rarity < b.rarity) return 1
    if (a.ivOrder > b.ivOrder) return -1
    if (a.ivOrder < b.ivOrder) return 1
    if (a.level > b.level) return -1
    if (a.level < b.level) return 1
    return 0
  })
}
