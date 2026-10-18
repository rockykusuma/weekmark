import Foundation
import SwiftUI
#if canImport(WidgetKit)
import WidgetKit
#endif

/// Shared between the app and its widget extension on each device (App Group), and across devices via iCloud.
enum AppGroup {
    static let id = "group.com.rockykusuma.weekmark"
    static var defaults: UserDefaults { UserDefaults(suiteName: id) ?? .standard }
}

/// Read-only view of the planning settings, cheap to build in a widget timeline.
struct PlanSnapshot {
    let numbering: WeekNumbering
    let regions: Set<HolidayRegion>
    let customHolidays: [Holiday]
    let sprint: SprintConfig
    let quarter: QuarterConfig
    let milestones: [Milestone]

    static func load(_ d: UserDefaults = AppGroup.defaults) -> PlanSnapshot {
        func json<T: Decodable>(_ key: String, _ fallback: T) -> T {
            let data = d.string(forKey: key)?.data(using: .utf8) ?? d.data(forKey: key)
            guard let data, let v = try? JSONDecoder().decode(T.self, from: data) else { return fallback }
            return v
        }
        let regions: Set<HolidayRegion>
        if let saved = d.array(forKey: "regions") as? [String] {
            regions = Set(saved.compactMap(HolidayRegion.init(rawValue:)))
        } else {
            regions = HolidayRegion.guess().map { [$0] } ?? []
        }
        return PlanSnapshot(numbering: WeekNumbering(rawValue: d.string(forKey: "numbering") ?? "") ?? .iso,
                            regions: regions,
                            customHolidays: json("customHolidays", []),
                            sprint: json("sprint", SprintConfig()),
                            quarter: json("quarter", QuarterConfig()),
                            milestones: json("milestones", []))
    }

    var calendar: Calendar { makeCalendar(numbering) }
    var holidays: HolidayStore { HolidayStore(regions: regions, custom: customHolidays) }

    func context(now: Date = Date()) -> QueryContext {
        QueryContext(cal: calendar, now: now, numbering: numbering, holidays: holidays,
                     milestones: milestones, sprint: sprint)
    }
}

/// Live, editable settings for the app UI. Every write goes to the App Group, iCloud, and reloads widgets.
@MainActor
final class PlanStore: ObservableObject {
    static let shared = PlanStore()

    private let d = AppGroup.defaults
    private var sync: SettingsSync?
    private var suppress = false

    @Published var numbering: WeekNumbering = .iso { didSet { save("numbering", numbering.rawValue) } }
    @Published var regions: Set<HolidayRegion> = [] { didSet { save("regions", regions.map(\.rawValue).sorted()) } }
    @Published var customHolidays: [Holiday] = [] { didSet { saveJSON("customHolidays", customHolidays) } }
    @Published var sprint = SprintConfig() { didSet { saveJSON("sprint", sprint) } }
    @Published var quarter = QuarterConfig() { didSet { saveJSON("quarter", quarter) } }
    @Published var milestones: [Milestone] = [] { didSet { saveJSON("milestones", milestones) } }
    @Published private(set) var lastSync: Date?
    @Published private(set) var iCloudAvailable = false

    private init() {
        reload()
        iCloudAvailable = FileManager.default.ubiquityIdentityToken != nil
        let s = SettingsSync(local: d, remote: NSUbiquitousKeyValueStore.default)
        s.onRemoteChange = { [weak self] _ in
            Task { @MainActor in self?.reload(); self?.lastSync = Date(); PlanStore.reloadWidgets() }
        }
        sync = s
        s.start()
        lastSync = Date()
    }

    var snapshot: PlanSnapshot { PlanSnapshot.load(d) }
    var calendar: Calendar { makeCalendar(numbering) }
    var holidays: HolidayStore { HolidayStore(regions: regions, custom: customHolidays) }
    func context(now: Date = Date()) -> QueryContext { snapshot.context(now: now) }

    func reload() {
        suppress = true
        let s = PlanSnapshot.load(d)
        numbering = s.numbering; regions = s.regions; customHolidays = s.customHolidays
        sprint = s.sprint; quarter = s.quarter; milestones = s.milestones
        suppress = false
    }

    func syncNow() {
        if let changed = sync?.syncNow(), !changed.isEmpty { reload(); Self.reloadWidgets() }
        lastSync = Date()
    }

    private func save(_ key: String, _ value: Any) {
        guard !suppress else { return }
        d.set(value, forKey: key)
        sync?.localDidChange(key)
        Self.reloadWidgets()
    }

    private func saveJSON<T: Encodable>(_ key: String, _ value: T) {
        guard !suppress, let data = try? JSONEncoder().encode(value) else { return }
        save(key, String(decoding: data, as: UTF8.self))
    }

    static func reloadWidgets() {
        #if canImport(WidgetKit)
        WidgetCenter.shared.reloadAllTimelines()
        #endif
    }
}

/// Widget extensions call this before building a timeline, so changes from other devices show up
/// even when the app hasn't been opened.
enum WidgetSync {
    static func pull() {
        let s = SettingsSync(local: AppGroup.defaults, remote: NSUbiquitousKeyValueStore.default)
        s.syncNow()
    }
}

extension MilestoneColor {
    var color: Color {
        switch self {
        case .orange: return .orange
        case .red: return .red
        case .pink: return .pink
        case .purple: return .purple
        case .blue: return .blue
        case .teal: return .teal
        case .green: return .green
        }
    }
}

/// Midnight-aligned timeline dates for widgets: now, then the next 7 midnights.
func widgetTimelineDates(from now: Date = Date(), cal: Calendar) -> [Date] {
    var dates = [now]
    var d = cal.startOfDay(for: now)
    for _ in 0..<7 {
        d = cal.date(byAdding: .day, value: 1, to: d)!
        dates.append(d)
    }
    return dates
}
