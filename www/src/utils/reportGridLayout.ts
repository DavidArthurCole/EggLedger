/**
 * Pure grid layout geometry for the report dashboard. Replicates CSS grid
 * auto-placement (row direction, no dense) so the app can reason about where
 * each report card lands, compute empty drop zones during drag-and-drop, and
 * resolve where a dragged card should be inserted. All functions are pure -
 * they take report definitions and occupancy as explicit arguments and hold no
 * reference to Vue reactive state.
 */
import type { ReportDefinition } from '../types/bridge'

/** Number of columns in the report grid. */
export const GRID_COLS = 8

/** Gap between grid cells, in pixels. */
export const GRID_GAP = 12

/** Resolved position and size of a placed report card. */
export interface GridCardPos {
  col: number
  row: number
  w: number
  h: number
  idx: number
}

/** A contiguous run of empty cells in a single grid row that can accept a drop. */
export interface EmptyZone {
  colStart: number
  colEnd: number
  rowStart: number
  insertAfter: number
}

/** Clamp a card's requested grid width/height to the valid range. */
export function clampDims(gridW: number, gridH: number): { w: number; h: number } {
  return { w: Math.min(Math.max(gridW, 1), GRID_COLS), h: Math.min(Math.max(gridH, 1), 8) }
}

/** Mark a w x h block of cells starting at (col, row) as occupied. */
export function markOccupied(occupied: Set<string>, col: number, row: number, w: number, h: number) {
  for (let rr = row; rr < row + h; rr++)
    for (let cc = col; cc < col + w; cc++) occupied.add(`${cc},${rr}`)
}

/** Whether a w x h block starting at (c, r) fits without overlapping occupied cells. */
export function cellsFit(occupied: Set<string>, c: number, r: number, w: number, h: number): boolean {
  for (let rr = r; rr < r + h; rr++)
    for (let cc = c; cc < c + w; cc++)
      if (occupied.has(`${cc},${rr}`)) return false
  return true
}

/** Replicates CSS grid auto-placement (row direction, no dense). */
export function findPlacement(occupied: Set<string>, w: number, h: number, col: number, row: number): { col: number; row: number } {
  let c = col
  let r = row
  while (true) {
    if (c + w - 1 > GRID_COLS) { c = 1; r++ }
    if (cellsFit(occupied, c, r, w, h)) return { col: c, row: r }
    c++
  }
}

/** Simulate placing a sequence of report defs and return the final cursor + occupancy. */
export function simulatePlacement(defs: ReportDefinition[]): { col: number; row: number; occupied: Set<string> } {
  const occupied = new Set<string>()
  let col = 1
  let row = 1
  for (const def of defs) {
    const { w, h } = clampDims(def.gridW, def.gridH)
    const pos = findPlacement(occupied, w, h, col, row)
    col = pos.col
    row = pos.row
    markOccupied(occupied, col, row, w, h)
    col += w
  }
  return { col, row, occupied }
}

/** Build the card positions + occupancy set for a displayed list of report defs. */
export function buildOccupancyFromLayout(defs: ReportDefinition[]): { cardPositions: GridCardPos[]; occupied: Set<string> } {
  const cardPositions: GridCardPos[] = []
  const occupied = new Set<string>()
  let col = 1
  let row = 1
  for (let idx = 0; idx < defs.length; idx++) {
    const def = defs[idx]
    const { w, h } = clampDims(def.gridW, def.gridH)
    const pos = findPlacement(occupied, w, h, col, row)
    col = pos.col
    row = pos.row
    cardPositions.push({ col, row, w, h, idx })
    markOccupied(occupied, col, row, w, h)
    col += w
  }
  return { cardPositions, occupied }
}

/** Index of the last card that precedes a zone's first cell, for insertion ordering. */
export function zoneInsertAfter(cardPositions: GridCardPos[], targetRow: number, runStart: number): number {
  const zoneFirst = (targetRow - 1) * GRID_COLS + runStart
  let best = -1
  for (const cp of cardPositions) {
    if ((cp.row - 1) * GRID_COLS + cp.col < zoneFirst) best = Math.max(best, cp.idx)
  }
  return best
}

/** Resolve the list index at which a dragged card should be inserted to land in a zone. */
export function findInsertIndexForZone(defs: ReportDefinition[], zone: EmptyZone, fromIdx: number): number {
  const draggedDef = defs[fromIdx]
  if (!draggedDef) return fromIdx

  const withoutDragged = defs.filter((_, i) => i !== fromIdx)
  const { w: dw, h: dh } = clampDims(draggedDef.gridW, draggedDef.gridH)

  for (let insertPos = 0; insertPos <= withoutDragged.length; insertPos++) {
    const { col, row, occupied } = simulatePlacement(withoutDragged.slice(0, insertPos))
    const pos = findPlacement(occupied, dw, dh, col, row)
    if (pos.col === zone.colStart && pos.row === zone.rowStart) return insertPos
  }

  // Fallback: use zone.insertAfter
  let insertAt = zone.insertAfter
  if (fromIdx <= insertAt) insertAt--
  return Math.max(insertAt + 1, 0)
}

/** Empty drop zones (contiguous empty cell runs) within a single grid row. */
export function rowEmptyZones(
  r: number,
  occupied: Set<string>,
  cardPositions: GridCardPos[],
): EmptyZone[] {
  const zones: EmptyZone[] = []
  let runStart: number | null = null
  for (let c = 1; c <= GRID_COLS + 1; c++) {
    const isEmpty = c <= GRID_COLS && !occupied.has(`${c},${r}`)
    if (isEmpty && runStart === null) { runStart = c; continue }
    if (!isEmpty && runStart !== null) {
      zones.push({
        colStart: runStart,
        colEnd: c,
        rowStart: r,
        insertAfter: zoneInsertAfter(cardPositions, r, runStart),
      })
      runStart = null
    }
  }
  return zones
}

/** Compute all empty drop zones across the rows occupied by the given report defs. */
export function computeEmptyZones(defs: ReportDefinition[]): EmptyZone[] {
  const { cardPositions, occupied } = buildOccupancyFromLayout(defs)
  if (cardPositions.length === 0) return []
  const maxRow = Math.max(...cardPositions.map(p => p.row + p.h - 1))
  const zones: EmptyZone[] = []
  for (let r = 1; r <= maxRow; r++) zones.push(...rowEmptyZones(r, occupied, cardPositions))
  return zones
}
