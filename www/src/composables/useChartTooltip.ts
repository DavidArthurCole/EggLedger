import { ref } from 'vue'

interface TooltipState {
  visible: boolean
  x: number
  y: number
  lines: string[]
}

export function useChartTooltip() {
  const tooltip = ref<TooltipState>({ visible: false, x: 0, y: 0, lines: [] })

  function showTooltip(e: MouseEvent, lines: string[]) {
    tooltip.value = { visible: true, x: e.clientX, y: e.clientY, lines }
  }

  function moveTooltip(e: MouseEvent) {
    if (!tooltip.value.visible) return
    tooltip.value.x = e.clientX
    tooltip.value.y = e.clientY
  }

  function hideTooltip() {
    tooltip.value.visible = false
  }

  return { tooltip, showTooltip, moveTooltip, hideTooltip }
}
