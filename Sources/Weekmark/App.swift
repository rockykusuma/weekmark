import AppKit
import SwiftUI
import Combine
import ServiceManagement
import CWCore

@main
enum WeekmarkMain {
    static let delegate = AppDelegate()
    static func main() {
        let app = NSApplication.shared
        app.delegate = delegate
        app.setActivationPolicy(.accessory) // no Dock icon
        app.run()
    }
}

/// Borderless panel; becomes key only when the week finder needs typing, without activating the app.
final class WidgetPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

final class WidgetHostingView<Content: View>: NSHostingView<Content> {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
}

final class MenuAction: NSMenuItem {
    private let handler: () -> Void
    init(_ title: String, checked: Bool = false, key: String = "", _ handler: @escaping () -> Void) {
        self.handler = handler
        super.init(title: title, action: #selector(fire), keyEquivalent: key)
        target = self
        state = checked ? .on : .off
    }
    required init(coder: NSCoder) { fatalError() }
    @objc private func fire() { handler() }
}

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate, NSMenuDelegate {
    let settings = Settings.shared
    let clock = Clock()
    lazy var browser = Browser(context: { [unowned self] in self.makeContext() },
                               snapshot: { [unowned self] in WeekSnapshot(now: self.clock.now, numbering: self.settings.numbering) })
    lazy var finder = QuickFinder(context: { [unowned self] in self.makeContext() })
    let router = SettingsRouter()
    private let hotKey = HotKey()

    private var panel: WidgetPanel!
    private var hosting: WidgetHostingView<WidgetView>!
    private var settingsWindow: NSWindow?
    private var onboardingWindow: NSWindow?
    private var statusItem: NSStatusItem!
    private let menu = NSMenu()
    private var cancellables = Set<AnyCancellable>()
    private var eventMonitor: Any?
    private var programmaticMove = false
    private var lastNumbering: WeekNumbering?
    private var lastHotkey: HotkeyPreset?
    private var scrollAcc: CGFloat = 0
    private var temporarilyFloated = false
    private var lastWeek: WeekRef?
    private let d = UserDefaults.standard

    func makeContext() -> QueryContext {
        QueryContext(cal: makeCalendar(settings.numbering), now: clock.now, numbering: settings.numbering,
                     holidays: settings.holidays, milestones: settings.milestones, sprint: settings.sprint)
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        menu.delegate = self
        _ = Updater.shared
        setupStatusItem()
        setupPanel()

        HotKey.action = { [weak self] in self?.finder.toggle() }
        finder.onShowInWidget = { [weak self] ref in self?.showFinder(selecting: ref) }
        registerHotkey()

        settings.objectWillChange
            .receive(on: RunLoop.main)
            .sink { [weak self] in DispatchQueue.main.async { self?.applySettings() } }
            .store(in: &cancellables)
        clock.$now
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in DispatchQueue.main.async { self?.tick() } }
            .store(in: &cancellables)

        NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification,
                                               object: nil, queue: .main) { [weak self] _ in self?.placeWindow() }
        NotificationCenter.default.addObserver(forName: .showOnboarding, object: nil, queue: .main) { [weak self] _ in
            self?.showOnboarding()
        }

        // Command line: open -a "Weekmark" --args --find 46   |   --settings <tab>   |   --onboarding
        let args = CommandLine.arguments
        if let i = args.firstIndex(of: "--find"), i + 1 < args.count {
            let q = args[i + 1]
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in self?.finder.show(query: q) }
        }
        if let i = args.firstIndex(of: "--settings") {
            let tab = i + 1 < args.count ? SettingsTab(rawValue: args[i + 1]) : nil
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in self?.showSettings(tab ?? .general) }
        }
        if !settings.onboardingDone || args.contains("--onboarding") {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in self?.showOnboarding() }
        }
    }

    /// Reopening the app (e.g. double-clicking it in Finder) opens Settings, since there is no Dock icon.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showSettings(.general)
        return false
    }

    private func tick() {
        updateStatusTitle()
        resetIfIdle()
        let w = makeCalendar(settings.numbering).weekRef(for: clock.now)
        if w != lastWeek { lastWeek = w; resizeToFit() } // milestones may have dropped off
    }

    private func registerHotkey() {
        guard lastHotkey != settings.hotkey else { return }
        lastHotkey = settings.hotkey
        hotKey.unregister()
        if let code = settings.hotkey.keyCode { _ = hotKey.register(keyCode: code, modifiers: settings.hotkey.modifiers) }
    }

    // MARK: Panel

    private func makeRoot() -> WidgetView {
        WidgetView(settings: settings, clock: clock, browser: browser, context: { [unowned self] in self.makeContext() })
    }

    private func setupPanel() {
        browser.onReset = { [weak self] in self?.endTemporaryFloat() }
        hosting = WidgetHostingView(rootView: makeRoot())
        hosting.sizingOptions = []
        hosting.wantsLayer = true
        hosting.layer?.backgroundColor = .clear

        panel = WidgetPanel(contentRect: NSRect(x: 0, y: 0, width: 324, height: 500),
                            styleMask: [.borderless, .nonactivatingPanel],
                            backing: .buffered, defer: false)
        panel.contentView = hosting
        panel.becomesKeyOnlyIfNeeded = true
        panel.acceptsMouseMovedEvents = true
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle, .fullScreenNone]
        panel.delegate = self

        eventMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown, .scrollWheel]) { [weak self] event in
            guard let self, event.window === self.panel else { return event }
            let p = event.locationInWindow
            let pt = CGPoint(x: p.x, y: self.hosting.bounds.height - p.y) // SwiftUI global coords are top-left
            if event.type == .scrollWheel {
                guard HitRegions.shared.contains(pt, id: "month") else { return event }
                self.handleScroll(event)
                return nil
            }
            if event.type == .rightMouseDown || event.modifierFlags.contains(.control) {
                NSMenu.popUpContextMenu(self.menu, with: event, for: self.hosting)
                return nil
            }
            if HitRegions.shared.contains(pt) { return event } // calendar / finder / milestones take clicks
            if !self.settings.locked {
                self.panel.performDrag(with: event)
                return nil
            }
            return event
        }

        applySettings(initial: true)
        panel.orderFrontRegardless()
    }

    private func applyLevel() {
        if temporarilyFloated { panel.level = .floating; return }
        switch settings.layer {
        case .desktop: panel.level = NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopIconWindow)) + 1)
        case .floating: panel.level = .floating
        }
    }

    private func applySettings(initial: Bool = false) {
        if let last = lastNumbering, last != settings.numbering { browser.reset() }
        lastNumbering = settings.numbering
        registerHotkey()
        applyLevel()
        switch settings.appearance {
        case .system: panel.appearance = nil
        case .light: panel.appearance = NSAppearance(named: .aqua)
        case .dark: panel.appearance = NSAppearance(named: .darkAqua)
        }
        resizeToFit()
        if initial { placeWindow() }
        updateStatusTitle()
    }

    /// Resize to SwiftUI's fitting size while keeping the top-left corner fixed.
    private func resizeToFit() {
        guard let panel else { return }
        let measure = NSHostingController(rootView: makeRoot())
        var fit = measure.sizeThatFits(in: CGSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude))
        fit = NSSize(width: ceil(fit.width), height: ceil(fit.height))
        guard fit.width > 10, fit.height > 10 else { return }
        var f = panel.frame
        guard f.size != fit else { return }
        let top = f.maxY
        f.size = fit
        f.origin.y = top - fit.height
        programmaticMove = true
        panel.setFrame(f, display: true)
        programmaticMove = false
    }

    // MARK: Placement (remembers the display, so it returns to your external monitor)

    private func displayID(_ screen: NSScreen) -> String? {
        guard let n = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber,
              let uuid = CGDisplayCreateUUIDFromDisplayID(n.uint32Value)?.takeRetainedValue() else { return nil }
        return CFUUIDCreateString(nil, uuid) as String
    }

    private func defaultScreen() -> NSScreen {
        let main = NSScreen.screens.first
        return NSScreen.screens.first(where: { $0 != main }) ?? main ?? NSScreen.main!
    }

    private func placeWindow() {
        let size = panel.frame.size
        var origin: NSPoint
        if let id = d.string(forKey: "screenID"),
           let screen = NSScreen.screens.first(where: { displayID($0) == id }),
           d.object(forKey: "offsetX") != nil {
            let ox = d.double(forKey: "offsetX"), oyTop = d.double(forKey: "offsetTop")
            origin = NSPoint(x: screen.frame.minX + ox, y: screen.frame.maxY - oyTop - size.height)
            origin = clamp(origin, size: size, in: screen.frame)
        } else {
            let vf = defaultScreen().visibleFrame
            origin = NSPoint(x: vf.maxX - size.width - 16, y: vf.maxY - size.height - 16)
        }
        programmaticMove = true
        panel.setFrameOrigin(origin)
        programmaticMove = false
    }

    private func clamp(_ p: NSPoint, size: NSSize, in r: NSRect) -> NSPoint {
        NSPoint(x: min(max(p.x, r.minX), r.maxX - size.width),
                y: min(max(p.y, r.minY), r.maxY - size.height))
    }

    private func savePosition() {
        guard let screen = panel.screen ?? NSScreen.screens.first(where: { $0.frame.intersects(panel.frame) }),
              let id = displayID(screen) else { return }
        d.set(id, forKey: "screenID")
        d.set(panel.frame.minX - screen.frame.minX, forKey: "offsetX")
        d.set(screen.frame.maxY - panel.frame.maxY, forKey: "offsetTop")
    }

    func windowDidMove(_ notification: Notification) {
        if (notification.object as? NSWindow) === panel, !programmaticMove { savePosition() }
    }

    private func move(to screen: NSScreen, corner: Int) {
        let vf = screen.visibleFrame, sz = panel.frame.size, m: CGFloat = 16
        let x = (corner % 2 == 0) ? vf.minX + m : vf.maxX - sz.width - m
        let y = (corner < 2) ? vf.maxY - sz.height - m : vf.minY + m
        panel.setFrameOrigin(NSPoint(x: x, y: y))
        savePosition()
    }

    // MARK: Browsing / lookup

    private func handleScroll(_ event: NSEvent) {
        if !event.momentumPhase.isEmpty { return }
        let dy = event.scrollingDeltaY
        if event.hasPreciseScrollingDeltas {
            if event.phase == .began { scrollAcc = 0 }
            scrollAcc += dy
            if abs(scrollAcc) > 35 { browser.step(scrollAcc > 0 ? -1 : 1); scrollAcc = 0 }
        } else if dy != 0 {
            browser.step(dy > 0 ? -1 : 1)
        }
    }

    /// Bring the widget in front of windows and select a week (or focus its search field).
    private func showFinder(selecting ref: WeekRef? = nil) {
        temporarilyFloated = true
        applyLevel()
        panel.orderFrontRegardless()
        if let ref { browser.select(ref) } else {
            panel.makeKey()
            browser.focusToken += 1
        }
        browser.touch()
    }

    private func endTemporaryFloat() {
        panel.makeFirstResponder(nil)
        if temporarilyFloated { temporarilyFloated = false; applyLevel() }
    }

    private func resetIfIdle() {
        guard browser.isBrowsing || temporarilyFloated else { return }
        if Date().timeIntervalSince(browser.lastInteraction) > 120 { browser.reset() }
    }

    // MARK: Windows

    func showSettings(_ tab: SettingsTab? = nil, prefill: WeekRef? = nil) {
        if let tab { router.tab = tab }
        if let prefill { router.prefillWeek = prefill }
        if settingsWindow == nil {
            let w = NSWindow(contentViewController: NSHostingController(rootView: SettingsView(settings: settings, router: router)))
            w.title = "Weekmark Settings"
            w.styleMask = [.titled, .closable, .miniaturizable]
            w.isReleasedWhenClosed = false
            w.center()
            settingsWindow = w
        }
        NSApp.activate(ignoringOtherApps: true)
        settingsWindow?.makeKeyAndOrderFront(nil)
    }

    func showOnboarding() {
        if onboardingWindow == nil {
            let view = OnboardingView(settings: settings) { [weak self] in
                self?.onboardingWindow?.close()
                self?.onboardingWindow = nil
            }
            let w = NSWindow(contentViewController: NSHostingController(rootView: view))
            w.title = "Welcome to Weekmark"
            w.styleMask = [.titled, .closable, .fullSizeContentView]
            w.titlebarAppearsTransparent = true
            w.isReleasedWhenClosed = false
            w.center()
            onboardingWindow = w
        }
        NSApp.activate(ignoringOtherApps: true)
        onboardingWindow?.makeKeyAndOrderFront(nil)
    }

    // MARK: Status item + menu

    private func setupStatusItem() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.menu = menu
        updateStatusTitle()
    }

    private func updateStatusTitle() {
        guard let button = statusItem?.button else { return }
        if settings.showInMenuBar {
            let ref = makeCalendar(settings.numbering).weekRef(for: clock.now)
            button.image = nil
            button.title = "CW \(ref.week)"
            button.font = NSFont.monospacedDigitSystemFont(ofSize: NSFont.systemFontSize, weight: .medium)
        } else {
            button.title = ""
            button.image = NSImage(systemSymbolName: "calendar", accessibilityDescription: "Weekmark")
        }
    }

    private func weeksMenu(year: Int, ctx: QueryContext) -> NSMenu {
        let m = NSMenu()
        let cal = ctx.cal, fmt = WeekFormatter(cal: cal)
        let font = NSFont.monospacedDigitSystemFont(ofSize: NSFont.systemFontSize, weight: .regular)
        let bold = NSFont.monospacedDigitSystemFont(ofSize: NSFont.systemFontSize, weight: .semibold)
        let msByWeek = Dictionary(grouping: settings.milestones, by: \.week)
        var lastMonth = ""
        for w in 1...cal.weeksIn(weekYear: year) {
            let ref = WeekRef(year: year, week: w)
            guard let start = cal.startOfWeek(ref), let mid = cal.date(byAdding: .day, value: 3, to: start) else { continue }
            let month = fmt.format(mid, "MMMM")
            if month != lastMonth {
                if !lastMonth.isEmpty { m.addItem(.separator()) }
                let h = NSMenuItem(title: month, action: nil, keyEquivalent: ""); h.isEnabled = false
                m.addItem(h)
                lastMonth = month
            }
            let item = MenuAction("", checked: ref == ctx.current) { [weak self] in self?.showFinder(selecting: ref) }
            var label = String(format: "CW %2d    ", w) + fmt.shortRange(ref)
            let wd = settings.holidays.workingDays(in: ref, cal: cal)
            if wd < 5 { label += "   · \(wd)d" }
            if let ms = msByWeek[ref] { label += "   ⚑ " + ms.map(\.name).joined(separator: ", ") }
            item.attributedTitle = NSAttributedString(string: label, attributes: [.font: ref == ctx.current ? bold : font])
            m.addItem(item)
        }
        return m
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()
        let ctx = makeContext()
        let snap = WeekSnapshot(now: clock.now, numbering: settings.numbering)
        let fmt = WeekFormatter(cal: ctx.cal)

        let header = NSMenuItem(title: "Week \(snap.weekNumber) of \(snap.weeksInYear) · \(snap.rangeText), \(snap.weekYear)", action: nil, keyEquivalent: "")
        header.isEnabled = false
        menu.addItem(header)
        if let q = Quarters.info(for: clock.now, config: settings.quarter, cal: ctx.cal) {
            var t = q.label
            if let sp = Sprints.info(for: clock.now, config: settings.sprint, cal: ctx.cal, holidays: settings.holidays) {
                t += " · \(settings.sprint.name) \(sp.number), day \(max(sp.workday, 1)) of \(sp.workdaysTotal)"
            }
            let i = NSMenuItem(title: t, action: nil, keyEquivalent: ""); i.isEnabled = false
            menu.addItem(i)
        }
        menu.addItem(MenuAction("Copy “\(fmt.copyText(ctx.current, currentYear: ctx.current.year))”", key: "c") {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(fmt.copyText(ctx.current, currentYear: ctx.current.year), forType: .string)
        })
        menu.addItem(.separator())

        // Right-clicked on a week row → quick milestone
        if let hover = browser.hover, panel.isVisible, panel.frame.contains(NSEvent.mouseLocation) {
            menu.addItem(MenuAction("Add Milestone in CW\(hover.week)…") { [weak self] in self?.showSettings(.milestones, prefill: hover) })
            menu.addItem(.separator())
        }

        let hk = settings.hotkey == .off ? "" : "   \(settings.hotkey.title)"
        menu.addItem(MenuAction("Quick Finder…\(hk)") { [weak self] in self?.finder.show() })
        for year in [snap.weekYear, snap.weekYear + 1] {
            let item = NSMenuItem(title: "Weeks in \(year)", action: nil, keyEquivalent: "")
            item.submenu = weeksMenu(year: year, ctx: ctx)
            menu.addItem(item)
        }
        menu.addItem(.separator())

        menu.addItem(MenuAction(panel.isVisible ? "Hide Widget" : "Show Widget") { [weak self] in
            guard let p = self?.panel else { return }
            p.isVisible ? p.orderOut(nil) : p.orderFrontRegardless()
        })
        let pos = NSMenu()
        for l in WindowLayer.allCases {
            pos.addItem(MenuAction(l.title, checked: settings.layer == l) { [weak self] in self?.settings.layer = l })
        }
        pos.addItem(.separator())
        pos.addItem(MenuAction("Lock Position", checked: settings.locked) { [weak self] in self?.settings.locked.toggle() })
        pos.addItem(.separator())
        let current = panel.screen ?? defaultScreen()
        for (i, name) in ["Top Left", "Top Right", "Bottom Left", "Bottom Right"].enumerated() {
            pos.addItem(MenuAction("Snap to \(name)") { [weak self] in self?.move(to: current, corner: i) })
        }
        if NSScreen.screens.count > 1 {
            pos.addItem(.separator())
            for screen in NSScreen.screens {
                pos.addItem(MenuAction("Move to \(screen.localizedName)", checked: screen == panel.screen) { [weak self] in
                    self?.move(to: screen, corner: 1)
                })
            }
        }
        let posItem = NSMenuItem(title: "Position", action: nil, keyEquivalent: ""); posItem.submenu = pos
        menu.addItem(posItem)
        let size = NSMenu()
        for v in WidgetSize.allCases {
            size.addItem(MenuAction(v.title, checked: settings.size == v) { [weak self] in self?.settings.size = v })
        }
        let sizeItem = NSMenuItem(title: "Size", action: nil, keyEquivalent: ""); sizeItem.submenu = size
        menu.addItem(sizeItem)

        menu.addItem(.separator())
        menu.addItem(MenuAction("Settings…", key: ",") { [weak self] in self?.showSettings() })
        menu.addItem(MenuAction("Check for Updates…") { Updater.shared.checkForUpdates() })
        menu.addItem(MenuAction("Quit Weekmark", key: "q") { NSApp.terminate(nil) })
    }
}
