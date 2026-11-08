import { createRoot } from 'react-dom/client'
import { WeekmarkHero } from './WeekmarkHero'

const el = document.getElementById('heroDemo')
if (el) createRoot(el).render(<WeekmarkHero />)
