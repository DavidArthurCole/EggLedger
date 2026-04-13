import { ref, computed } from 'vue'
import type { Ref } from 'vue'
import type { DatabaseMission } from '../types/bridge'

export interface GroupedYear {
  year: number
  enabled: boolean
}

export interface GroupedMonth {
  month: number
  enabled: boolean
}

export interface GroupedDay {
  day: number
  enabled: boolean
}

export interface GroupedArrays {
  year: GroupedYear[]
  month: GroupedMonth[][]
  day: GroupedDay[][][]
}

export function useMissionListGrouping(
  tabFilteredMissions: Ref<DatabaseMission[] | null>,
  ledgerDate: (timestamp: number) => Date,
  collapseOlderSections: Ref<boolean>,
  viewByDate: Ref<boolean>,
) {
  const groupedArrays = ref<GroupedArrays>({ year: [], month: [], day: [] })
  const allVisible = ref(true)

  // Single O(N) pass: group missions year -> month -> day rather than repeated filter scans.
  // groupedArrays is updated alongside groupedMissions to reset expand-collapse state
  // when filters change. The assignment below is an intentional side effect.
  // eslint-disable-next-line sonarjs/cognitive-complexity
  const groupedMissions = computed(() => {
    const fm = tabFilteredMissions.value
    if (fm == null || fm.length === 0) return [] as DatabaseMission[][][][]

    // Build a nested Map: year -> month -> day -> missions[] in one pass
    const dateMap = new Map<number, Map<number, Map<number, DatabaseMission[]>>>()
    for (const mission of fm) {
      const d = ledgerDate(mission.launchDT)
      const y = d.getFullYear()
      const mo = d.getMonth() + 1
      const da = d.getDate()
      if (!dateMap.has(y)) dateMap.set(y, new Map())
      const ym = dateMap.get(y)!
      if (!ym.has(mo)) ym.set(mo, new Map())
      const md = ym.get(mo)!
      if (!md.has(da)) md.set(da, [])
      md.get(da)!.push(mission)
    }

    const uniqueYears = [...dateMap.keys()].sort((a, b) => b - a)
    const uniqueMonthsArr = uniqueYears.map((y) => [...dateMap.get(y)!.keys()].sort((a, b) => b - a))
    const uniqueDaysArr = uniqueYears.map((y, yi) =>
      uniqueMonthsArr[yi].map((mo) => [...dateMap.get(y)!.get(mo)!.keys()].sort((a, b) => b - a)),
    )

    const collapse = collapseOlderSections.value
    // eslint-disable-next-line vue/no-side-effects-in-computed-properties
    groupedArrays.value.year = uniqueYears.map((year, yi) => ({ year, enabled: !collapse || yi === 0 }))
    // eslint-disable-next-line vue/no-side-effects-in-computed-properties
    groupedArrays.value.month = uniqueMonthsArr.map((months, yi) =>
      months.map((month) => ({ month, enabled: !collapse || yi === 0 })),
    )
    // eslint-disable-next-line vue/no-side-effects-in-computed-properties
    groupedArrays.value.day = uniqueDaysArr.map((year, yi) =>
      year.map((month) => month.map((day) => ({ day, enabled: !collapse || yi === 0 }))),
    )
    // eslint-disable-next-line vue/no-side-effects-in-computed-properties
    allVisible.value = !collapse || uniqueYears.length <= 1

    return uniqueYears.map((y, yi) =>
      uniqueMonthsArr[yi].map((mo, mi) =>
        uniqueDaysArr[yi][mi].map((da) => [...dateMap.get(y)!.get(mo)!.get(da)!].reverse()),
      ),
    )
  })

  function isDayRowVisible(yearIndex: number, monthIndex: number, dayIndex: number): boolean {
    if (viewByDate.value) return groupedArrays.value.day[yearIndex][monthIndex][dayIndex].enabled
    return groupedArrays.value.month[yearIndex][monthIndex].enabled
  }

  // eslint-disable-next-line sonarjs/cognitive-complexity
  function toggleElements(
    _event: Event,
    passedYear?: GroupedYear,
    passedMonth?: GroupedMonth,
    passedDay?: GroupedDay,
  ) {
    const yA = groupedArrays.value.year
    const mA = groupedArrays.value.month
    const dA = groupedArrays.value.day
    if (passedYear) {
      const yearObj = yA[yA.indexOf(passedYear)]
      if (passedMonth) {
        const yIdx = yA.indexOf(passedYear)
        const monthObj = mA[yIdx][mA[yIdx].indexOf(passedMonth)]
        if (passedDay) {
          const mIdx = mA[yIdx].indexOf(passedMonth)
          const dayObj = dA[yIdx][mIdx][dA[yIdx][mIdx].indexOf(passedDay)]
          dayObj.enabled = !dayObj.enabled
        } else monthObj.enabled = !monthObj.enabled
      } else yearObj.enabled = !yearObj.enabled
    } else {
      allVisible.value = !allVisible.value
      for (let yi = 0; yi < yA.length; yi++) {
        yA[yi].enabled = allVisible.value
        for (let mi = 0; mi < mA[yi].length; mi++) {
          mA[yi][mi].enabled = allVisible.value
          for (const day of dA[yi][mi]) {
            day.enabled = allVisible.value
          }
        }
      }
    }
  }

  return {
    groupedArrays,
    allVisible,
    groupedMissions,
    isDayRowVisible,
    toggleElements,
  }
}
