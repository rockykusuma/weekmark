import AppKit
import CWCore

/// Publishes the current time; ticks every 30s and immediately on day/clock/timezone change or wake.
final class Clock: ObservableObject {
    @Published var now = Date()
    private var timer: Timer?

    init() {
        timer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in self?.tick() }
        timer?.tolerance = 5
        let nc = NotificationCenter.default
        for name in [Notification.Name.NSCalendarDayChanged, .NSSystemClockDidChange, .NSSystemTimeZoneDidChange] {
            nc.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in self?.tick() }
        }
        NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in self?.tick() }
    }

    func tick() { now = Date() }
}

/// State for browsing months and looking up a week inside the widget.
final class Browser: ObservableObject {
    @Published var monthOffset = 0
    @Published var target: WeekRef?
    @Published var hover: WeekRef?
    @Published var result: QueryResult?
    @Published var focusToken = 0
    @Published var query = "" { didSet { if !suppressLive { liveSearch() } } }

    var lastInteraction = Date()
    var onReset: (() -> Void)?
    private var suppressLive = false
    private let context: () -> QueryContext
    private let snapshot: () -> WeekSnapshot

    init(context: @escaping () -> QueryContext, snapshot: @escaping () -> WeekSnapshot) {
        self.context = context
        self.snapshot = snapshot
    }

    var isBrowsing: Bool { monthOffset != 0 || target != nil || !query.isEmpty }

    func touch() { lastInteraction = Date() }

    func step(_ delta: Int) {
        monthOffset += delta
        touch()
    }

    func select(_ ref: WeekRef, clearQuery: Bool = true) {
        target = ref
        result = QueryEngine.weekResult(ref, context())
        if let off = snapshot().monthOffset(for: ref) { monthOffset = off }
        if clearQuery { setQuerySilently("") }
        touch()
    }

    func reset() {
        setQuerySilently("")
        monthOffset = 0
        target = nil
        hover = nil
        result = nil
        onReset?()
    }

    private func liveSearch() {
        touch()
        let q = query.trimmingCharacters(in: .whitespaces)
        guard !q.isEmpty else { target = nil; result = nil; monthOffset = 0; return }
        let r = QueryEngine.evaluate(q, context()).first
        result = r
        if let ref = r?.week {
            target = ref
            if let off = snapshot().monthOffset(for: ref) { monthOffset = off }
        } else {
            target = nil
        }
    }

    private func setQuerySilently(_ q: String) {
        suppressLive = true
        query = q
        suppressLive = false
    }
}
