import { CalendarClock, Calendar, ChevronLeft, ChevronRight, Flag, Layers, PieChart, RefreshCw, Search } from 'lucide-react'
import { Stage } from './kit/stage'
import { Collapse } from './kit/motion/Collapse'
import { Popover } from './kit/motion/Popover'
import { Roll } from './kit/motion/Roll'
import type { Act } from './kit/act'
import './weekmark-hero.css'

// Weekmark's hero: the widget on a Mac desktop. ⌃⌥W opens Quick Finder,
// "47" answers with the week and its milestone, ⌘⏎ shows it in the widget
// (the month grid turns to November, CW47 outlined), Today puts it back.
// Strings match the Mac app (CWCore/QueryEngine, WidgetView) on 26 Oct 2026.

type S = { open: boolean; query: string; result: boolean; target: boolean }
const initial: S = { open: false, query: '', result: false, target: false }
const POSTER: S = { open: true, query: '47', result: true, target: false }

const PURPLE = '#af52de'
const ORANGE = '#ff9500'

type Week = { cw: number; days: number[]; out: number[]; flag?: string; holiday?: number }
// days: day numbers Mon..Sun; out: indexes outside the shown month
const OCTOBER: Week[] = [
  { cw: 40, days: [28, 29, 30, 1, 2, 3, 4], out: [0, 1, 2], holiday: 4 },
  { cw: 41, days: [5, 6, 7, 8, 9, 10, 11], out: [] },
  { cw: 42, days: [12, 13, 14, 15, 16, 17, 18], out: [] },
  { cw: 43, days: [19, 20, 21, 22, 23, 24, 25], out: [] },
  { cw: 44, days: [26, 27, 28, 29, 30, 31, 1], out: [6] },
  { cw: 45, days: [2, 3, 4, 5, 6, 7, 8], out: [0, 1, 2, 3, 4, 5, 6] },
]
const NOVEMBER: Week[] = [
  { cw: 44, days: [26, 27, 28, 29, 30, 31, 1], out: [0, 1, 2, 3, 4, 5] },
  { cw: 45, days: [2, 3, 4, 5, 6, 7, 8], out: [] },
  { cw: 46, days: [9, 10, 11, 12, 13, 14, 15], out: [] },
  { cw: 47, days: [16, 17, 18, 19, 20, 21, 22], out: [], flag: PURPLE },
  { cw: 48, days: [23, 24, 25, 26, 27, 28, 29], out: [] },
  { cw: 49, days: [30, 1, 2, 3, 4, 5, 6], out: [1, 2, 3, 4, 5, 6], flag: ORANGE },
]
const SPRINT_START = new Set([40, 42, 44, 46, 48])

async function script(a: Act<S>) {
  await a.wait(700)
  a.mark('hotkey')
  await a.key('ctrl+alt+w')
  await a.set({ open: true })
  await a.wait(500)
  a.mark('type')
  await a.type('query', '47', { interval: 140 })
  await a.set({ result: true })
  await a.wait(1900)
  a.mark('show')
  await a.key('cmd+enter')
  await a.set({ open: false })
  await a.wait(120)
  await a.set({ target: true })
  // Clear the closed panel while nobody can see it
  await a.wait(400)
  await a.snap({ query: '', result: false })
  await a.wait(1800)
  a.mark('today')
  await a.move({ at: 'today', x: 0.86, y: 0.7 })
  await a.wait(250)
  await a.click()
  await a.set({ target: false })
  await a.wait(500)
  await a.move(a.layout === 'wide' ? { x: 70, y: 520 } : { x: 30, y: 300 })
  await a.wait(700)
}

export function WeekmarkHero() {
  return (
    <Stage
      width={480}
      height={690}
      compact={{ below: 400, width: 358, height: 590 }}
      initial={initial}
      poster={POSTER}
      script={script}
      greys="cool"
      keys={{ wide: 'bottom-left', compact: 'top' }}
      cursor={{ at: { wide: { x: 70, y: 520 }, compact: { x: 30, y: 300 } } }}
      className="wm"
    >
      {(s, _rect, layout) => (
        <div className="wm-desk">
          <Widget s={s} />
          <Popover show={s.open} origin="top" className="wm-finder">
            <div className="wm-finder-bar">
              <CalendarClock className="wm-finder-icon" size={20} strokeWidth={2} />
              <span className="wm-finder-input" data-target="query">
                <span>{s.query}</span>
                <span className="wm-caret" />
                <span className="wm-finder-placeholder" data-hidden={!!s.query || undefined}>
                  {layout === 'wide' ? '46 · Dec 12 · +6w · 46-50' : '46 · Dec 12 · +6w'}
                </span>
              </span>
              <span className="wm-finder-now">CW44</span>
            </div>
            <Collapse open={s.result}>
              <div className="wm-finder-results">
                <div className="wm-result">
                  <span className="wm-result-icon">
                    <Calendar size={15} strokeWidth={2.4} />
                  </span>
                  <span className="wm-result-text">
                    <span className="wm-result-title">CW47 · Mon 16 Nov – Sun 22 Nov 2026</span>
                    <span className="wm-result-sub">In 3 weeks · November · 5 working days</span>
                    <span className="wm-result-detail">◆ Code freeze</span>
                  </span>
                  {layout === 'wide' && <span className="wm-result-copy">⏎ Copy</span>}
                </div>
              </div>
            </Collapse>
            <div className="wm-finder-foot">
              <Hint k="⏎" label="Copy" />
              <Hint k="⌘⏎" label="Show in widget" />
              {layout === 'wide' && <Hint k="esc" label="Close" />}
            </div>
          </Popover>
        </div>
      )}
    </Stage>
  )
}

function Hint({ k, label }: { k: string; label: string }) {
  return (
    <span className="wm-hint">
      <kbd>{k}</kbd>
      {label}
    </span>
  )
}

function Widget({ s }: { s: S }) {
  return (
    <div className="wm-widget">
      <div className="wm-head">
        <div>
          <div className="wm-eyebrow">CALENDAR WEEK</div>
          <div className="wm-cw">44</div>
          <div className="wm-range">26 Oct – 1 Nov</div>
        </div>
        <div className="wm-year">
          <span>2026</span>
          <span className="wm-ring">
            <svg viewBox="0 0 50 50" aria-hidden>
              <circle cx="25" cy="25" r="22.5" className="wm-ring-track" />
              <circle cx="25" cy="25" r="22.5" className="wm-ring-arc" pathLength={53} strokeDasharray="44 53" />
            </svg>
            <span className="wm-ring-num">
              <b>44</b>
              <small>of 53</small>
            </span>
          </span>
          <span>9 wks left</span>
        </div>
      </div>

      <div className="wm-chips">
        <span className="wm-chip">
          <PieChart size={10} strokeWidth={2.6} />
          Q4 2026
        </span>
        <span className="wm-chip">
          <RefreshCw size={10} strokeWidth={2.6} />
          Sprint 22 · day 1/10
        </span>
        <span className="wm-chip">
          <Layers size={10} strokeWidth={2.6} />
          PI 5 · 2/5
        </span>
      </div>

      <div className="wm-strip">
        {['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'].map((d, i) => (
          <div key={d} className="wm-day" data-today={i === 0 || undefined} data-weekend={i > 4 || undefined}>
            <span>{d}</span>
            <b>{[26, 27, 28, 29, 30, 31, 1][i]}</b>
          </div>
        ))}
      </div>

      <div className="wm-milestones">
        <Milestone color={PURPLE} name="Code freeze" cw={47} left="3w · 20d" />
        <Milestone color={ORANGE} name="Release 5.0" cw={49} left="5w · 30d" />
      </div>

      <div className="wm-rule" />

      <div className="wm-month">
        <div className="wm-month-head">
          <span className="wm-month-title">
            <Roll>{s.target ? 'November 2026' : 'October 2026'}</Roll>
          </span>
          <span className="wm-today" data-show={s.target || undefined}>
            <span className="wm-pill" data-target="today" data-cursor="pointer">
              Today
            </span>
          </span>
          <span className="wm-pill wm-pill--sq">
            <Calendar size={11} strokeWidth={2.4} />
          </span>
          <span className="wm-pill wm-pill--sq">
            <ChevronLeft size={12} strokeWidth={2.6} />
          </span>
          <span className="wm-pill wm-pill--sq">
            <ChevronRight size={12} strokeWidth={2.6} />
          </span>
        </div>
        <div className="wm-grid-head">
          <span>CW</span>
          {['M', 'T', 'W', 'T', 'F', 'S', 'S'].map((d, i) => (
            <span key={i}>{d}</span>
          ))}
        </div>
        <div className="wm-grids">
          <Grid weeks={OCTOBER} show={!s.target} />
          <Grid weeks={NOVEMBER} show={s.target} target={47} />
        </div>
      </div>

      {/* Below the crop: the widget's own find field runs on past the desktop's edge */}
      <div className="wm-find">
        <Search size={11} strokeWidth={2.6} />
        <span>46 · Dec 12 · +6w · 46-50</span>
      </div>
      <div className="wm-find-hint">Type a week, date or offset — or press ⌃⌥W anywhere.</div>
    </div>
  )
}

function Milestone({ color, name, cw, left }: { color: string; name: string; cw: number; left: string }) {
  return (
    <div className="wm-ms">
      <Flag size={10} fill={color} color={color} strokeWidth={2} />
      <span className="wm-ms-name">{name}</span>
      <span className="wm-ms-cw" style={{ color }}>
        CW{cw}
      </span>
      <span className="wm-ms-left">{left}</span>
    </div>
  )
}

function Grid({ weeks, show, target }: { weeks: Week[]; show: boolean; target?: number }) {
  return (
    <div className="wm-grid" data-show={show || undefined}>
      {weeks.map((w) => (
        <div
          key={w.cw}
          className="wm-week"
          data-current={w.cw === 44 || undefined}
          data-target={w.cw === target ? 'target-week' : undefined}
          data-lookup={w.cw === target || undefined}
          data-sprint={SPRINT_START.has(w.cw) || undefined}
        >
          <span className="wm-week-cw" style={w.flag && w.cw !== target ? { color: w.flag } : undefined}>
            {w.flag && <Flag size={8} fill={w.flag} color={w.flag} strokeWidth={2} />}
            {w.cw}
          </span>
          {w.days.map((d, i) => (
            <span
              key={i}
              className="wm-date"
              data-out={w.out.includes(i) || undefined}
              data-today={(w.cw === 44 && i === 0) || undefined}
              data-holiday={w.holiday === i || undefined}
            >
              {d}
            </span>
          ))}
        </div>
      ))}
    </div>
  )
}
