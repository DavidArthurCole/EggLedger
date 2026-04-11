import { createApp } from 'vue'
import App from './App.vue'
import './index.css'

const app = createApp(App)

// v-unhide: removes style="display:none" on mount to prevent FOUC
app.directive('unhide', {
  mounted(el: HTMLElement) {
    el.style.removeProperty('display')
  },
})

// v-external-link: opens href via the Go shell (lorca openURL binding)
// Use on <a> tags to prevent in-app navigation.
app.directive('external-link', {
  mounted(el: HTMLElement) {
    el.addEventListener('click', (e: Event) => {
      e.preventDefault()
      const href = (el as HTMLAnchorElement).href
      if (href) globalThis.openURL(href)
    })
  },
})

app.mount('#app')
