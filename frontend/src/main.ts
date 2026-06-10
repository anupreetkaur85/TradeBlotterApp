import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { readStoredTheme } from './composables/useTheme'
import './styles/main.css'

// Apply the saved theme before mount to avoid a flash of the wrong theme.
document.documentElement.setAttribute('data-theme', readStoredTheme())

createApp(App).use(createPinia()).mount('#app')
