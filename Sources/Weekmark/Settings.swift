import SwiftUI
import Carbon.HIToolbox
import CWCore

enum WidgetSize: String, CaseIterable {
    case small, medium, large, xlarge
    var scale: CGFloat {
        switch self {
        case .small: return 0.8
        case .medium: return 1.0
        case .large: return 1.3
        case .xlarge: return 1.7
        }
    }
    var title: String {
        switch self {
        case .small: return "Small"
        case .medium: return "Medium"
        case .large: return "Large"
        case .xlarge: return "Extra Large"
        }
    }
}

enum WindowLayer: String, CaseIterable {
    case desktop, floating
    var title: String { self == .desktop ? "On Desktop (behind windows)" : "Float Above Windows" }
}

enum BackgroundStyle: String, CaseIterable {
    case glass, solid, clear
    var title: String { rawValue.capitalized }
}

enum AppearanceMode: String, CaseIterable {
    case system, light, dark
    var title: String { rawValue.capitalized }
}

enum HotkeyPreset: String, CaseIterable {
    case ctrlOptW, optCmdW, ctrlOptCmdW, ctrlOptSpace, off
    var title: String {
        switch self {
        case .ctrlOptW: return "⌃⌥W"
        case .optCmdW: return "⌥⌘W"
        case .ctrlOptCmdW: return "⌃⌥⌘W"
        case .ctrlOptSpace: return "⌃⌥Space"
        case .off: return "Off"
        }
    }
    var keyCode: UInt32? {
        switch self {
        case .ctrlOptW, .optCmdW, .ctrlOptCmdW: return UInt32(kVK_ANSI_W)
        case .ctrlOptSpace: return UInt32(kVK_Space)
        case .off: return nil
        }
    }
    var modifiers: UInt32 {
        switch self {
        case .ctrlOptW, .ctrlOptSpace: return UInt32(controlKey | optionKey)
        case .optCmdW: return UInt32(optionKey | cmdKey)
        case .ctrlOptCmdW: return UInt32(controlKey | optionKey | cmdKey)
        case .off: return 0
        }
    }
}

/// All preferences. Complex values are stored as JSON strings so IT can also set them via an MDM profile
/// (managed preferences for com.rockykusuma.weekmark override user values automatically).
final class Settings: ObservableObject {
    static let shared = Settings()
    private let d = UserDefaults.standard

    // Widget
    @Published var numbering: WeekNumbering { didSet { d.set(numbering.rawValue, forKey: "numbering") } }
    @Published var size: WidgetSize { didSet { d.set(size.rawValue, forKey: "size") } }
    @Published var layer: WindowLayer { didSet { d.set(layer.rawValue, forKey: "layer") } }
    @Published var background: BackgroundStyle { didSet { d.set(background.rawValue, forKey: "background") } }
    @Published var appearance: AppearanceMode { didSet { d.set(appearance.rawValue, forKey: "appearance") } }
    @Published var showMonth: Bool { didSet { d.set(showMonth, forKey: "showMonth") } }
    @Published var showProgress: Bool { didSet { d.set(showProgress, forKey: "showProgress") } }
    @Published var showPlanning: Bool { didSet { d.set(showPlanning, forKey: "showPlanning") } }
    @Published var showMilestones: Bool { didSet { d.set(showMilestones, forKey: "showMilestones") } }
    @Published var showInMenuBar: Bool { didSet { d.set(showInMenuBar, forKey: "showInMenuBar") } }
    @Published var locked: Bool { didSet { d.set(locked, forKey: "locked") } }

    // General
    @Published var hotkey: HotkeyPreset { didSet { d.set(hotkey.rawValue, forKey: "hotkey") } }
    @Published var onboardingDone: Bool { didSet { d.set(onboardingDone, forKey: "onboardingDone") } }

    // Planning
    @Published var sprint: SprintConfig { didSet { save(sprint, "sprint") } }
    @Published var quarter: QuarterConfig { didSet { save(quarter, "quarter") } }
    @Published var milestones: [Milestone] { didSet { save(milestones, "milestones") } }

    // Holidays
    @Published var regions: Set<HolidayRegion> { didSet { d.set(regions.map(\.rawValue).sorted(), forKey: "regions"); rebuildHolidays() } }
    @Published var customHolidays: [Holiday] { didSet { save(customHolidays, "customHolidays"); rebuildHolidays() } }
    private(set) var holidays: HolidayStore = .empty

    /// One-time copy of preferences from the pre-rename bundle id (Calendar Week → Weekmark).
    private static func migrateLegacyDefaults() {
        let d = UserDefaults.standard
        guard !d.bool(forKey: "didMigrateLegacy") else { return }
        if let old = d.persistentDomain(forName: "com.rockykusuma.calendarweek") {
            for (k, v) in old where d.object(forKey: k) == nil { d.set(v, forKey: k) }
        }
        d.set(true, forKey: "didMigrateLegacy")
    }

    private init() {
        Settings.migrateLegacyDefaults()
        d.register(defaults: ["showMonth": true, "showProgress": true, "showInMenuBar": true, "locked": false,
                              "showPlanning": true, "showMilestones": true, "onboardingDone": false])
        numbering = WeekNumbering(rawValue: d.string(forKey: "numbering") ?? "") ?? .iso
        size = WidgetSize(rawValue: d.string(forKey: "size") ?? "") ?? .medium
        layer = WindowLayer(rawValue: d.string(forKey: "layer") ?? "") ?? .desktop
        background = BackgroundStyle(rawValue: d.string(forKey: "background") ?? "") ?? .glass
        appearance = AppearanceMode(rawValue: d.string(forKey: "appearance") ?? "") ?? .system
        showMonth = d.bool(forKey: "showMonth")
        showProgress = d.bool(forKey: "showProgress")
        showPlanning = d.bool(forKey: "showPlanning")
        showMilestones = d.bool(forKey: "showMilestones")
        showInMenuBar = d.bool(forKey: "showInMenuBar")
        locked = d.bool(forKey: "locked")
        hotkey = HotkeyPreset(rawValue: d.string(forKey: "hotkey") ?? "") ?? .ctrlOptW
        onboardingDone = d.bool(forKey: "onboardingDone")
        sprint = Settings.load("sprint", SprintConfig())
        quarter = Settings.load("quarter", QuarterConfig())
        milestones = Settings.load("milestones", [])
        customHolidays = Settings.load("customHolidays", [])
        if let saved = d.array(forKey: "regions") as? [String] {
            regions = Set(saved.compactMap(HolidayRegion.init(rawValue:)))
        } else {
            regions = HolidayRegion.guess().map { [$0] } ?? []
        }
        rebuildHolidays()
    }

    private func rebuildHolidays() { holidays = HolidayStore(regions: regions, custom: customHolidays) }

    private func save<T: Encodable>(_ v: T, _ key: String) {
        if let data = try? JSONEncoder().encode(v) { d.set(String(decoding: data, as: UTF8.self), forKey: key) }
    }

    private static func load<T: Decodable>(_ key: String, _ fallback: T) -> T {
        let d = UserDefaults.standard
        let data: Data? = (d.string(forKey: key)?.data(using: .utf8)) ?? d.data(forKey: key)
        guard let data, let v = try? JSONDecoder().decode(T.self, from: data) else { return fallback }
        return v
    }

    func isManaged(_ key: String) -> Bool { d.objectIsForced(forKey: key) }
}
