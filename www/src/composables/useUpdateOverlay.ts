import { ref } from 'vue'
import type { Ref } from 'vue'

type OverlayMode = 'idle' | 'updating' | 'countdown'

const overlayMode: Ref<OverlayMode> = ref('idle')
const countdownValue = ref(0)
let _timer: ReturnType<typeof setInterval> | null = null

function clearTimer() {
  if (_timer !== null) {
    clearInterval(_timer)
    _timer = null
  }
}

export function useUpdateOverlay() {
  function begin() {
    clearTimer()
    countdownValue.value = 0
    overlayMode.value = 'updating'
  }

  function beginCountdown(seconds: number) {
    clearTimer()
    overlayMode.value = 'countdown'
    countdownValue.value = seconds
    _timer = setInterval(() => {
      if (countdownValue.value <= 1) {
        countdownValue.value = 0
        clearTimer()
        return
      }
      countdownValue.value -= 1
    }, 1000)
  }

  function end() {
    clearTimer()
    countdownValue.value = 0
    overlayMode.value = 'idle'
  }

  return {
    overlayMode,
    countdownValue,
    begin,
    beginCountdown,
    end,
  }
}
