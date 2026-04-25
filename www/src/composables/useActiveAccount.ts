import { ref, readonly } from 'vue'

const _activeAccountId = ref<string | null>(null)

export function useActiveAccount() {
  async function initActiveAccount() {
    const id = await globalThis.getActiveAccountId()
    if (id && id !== '') {
      _activeAccountId.value = id
    }
  }

  function setActive(id: string) {
    _activeAccountId.value = id
    void globalThis.setActiveAccountId(id)
  }

  return {
    activeAccountId: readonly(_activeAccountId),
    initActiveAccount,
    setActive,
  }
}
