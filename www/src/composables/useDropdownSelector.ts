import { ref, onMounted, onUnmounted } from 'vue'

/**
 * Shared dropdown selector behavior: open/close state, container ref for
 * containment checks, and automatic click-outside-to-close lifecycle wiring.
 *
 * @param onSelect - Called with the selected value when close(value) is
 *   invoked with a non-empty string. Use this to update the caller's own
 *   value ref and trigger any follow-up work.
 */
export function useDropdownSelector(onSelect?: (value: string) => void) {
  const containerRef = ref<HTMLElement | null>(null)
  const isOpen = ref(false)

  function open() {
    isOpen.value = true
  }

  function close(value?: string) {
    if (value != null && value !== '') {
      onSelect?.(value)
    }
    isOpen.value = false
  }

  function handleClickOutside(event: MouseEvent) {
    if (
      isOpen.value &&
      containerRef.value &&
      !containerRef.value.contains(event.target as Node)
    ) {
      isOpen.value = false
    }
  }

  onMounted(() => {
    document.addEventListener('mousedown', handleClickOutside)
  })

  onUnmounted(() => {
    document.removeEventListener('mousedown', handleClickOutside)
  })

  return { containerRef, isOpen, open, close }
}
