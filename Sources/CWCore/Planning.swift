import Foundation

// MARK: - Sprints & Program Increments

public struct SprintConfig: Codable, Equatable, Sendable {
    public var enabled: Bool = false
    public var lengthWeeks: Int = 2
    /// Any date in the first sprint's first week; sprints start on the calendar's first weekday.
    public var anchor: Day = Day(2026, 1, 5)
    public var firstNumber: Int = 1
    public var name: String = "Sprint"
    /// 0 = Program Increments off. SAFe commonly uses 5 (4 dev + 1 IP).
    public var sprintsPerPI: Int = 0
    public var firstPI: Int = 1
    public init() {}
}

public struct SprintInfo: Equatable, Sendable {
    public let number: Int
    public let start: Date          // inclusive
    public let end: Date            // exclusive
    public let firstWeek: WeekRef
    public let lastWeek: WeekRef
    public let workday: Int         // 1-based working day today within the sprint (0 if before first workday)
    public let workdaysTotal: Int
    public let pi: Int?
    public let sprintInPI: Int?
    public let sprintsPerPI: Int
}

public enum Sprints {
    public static func index(for date: Date, config: SprintConfig, cal: Calendar) -> Int? {
        guard config.enabled, config.lengthWeeks > 0,
              let anchorDate = config.anchor.date(in: cal),
              let anchorStart = cal.dateInterval(of: .weekOfYear, for: anchorDate)?.start else { return nil }
        let days = cal.dateComponents([.day], from: anchorStart, to: cal.startOfDay(for: date)).day ?? 0
        let len = config.lengthWeeks * 7
        return Int(floor(Double(days) / Double(len)))
    }

    public static func info(for date: Date, config: SprintConfig, cal: Calendar, holidays: HolidayStore) -> SprintInfo? {
        guard let idx = index(for: date, config: config, cal: cal),
              let anchorDate = config.anchor.date(in: cal),
              let anchorStart = cal.dateInterval(of: .weekOfYear, for: anchorDate)?.start,
              let start = cal.date(byAdding: .weekOfYear, value: idx * config.lengthWeeks, to: anchorStart),
              let end = cal.date(byAdding: .weekOfYear, value: config.lengthWeeks, to: start),
              let lastDay = cal.date(byAdding: .day, value: -1, to: end) else { return nil }
        let total = holidays.workingDays(from: start, to: end, cal: cal)
        let tomorrow = cal.date(byAdding: .day, value: 1, to: cal.startOfDay(for: date))!
        let done = holidays.workingDays(from: start, to: tomorrow, cal: cal)
        var pi: Int?, inPI: Int?
        if config.sprintsPerPI > 0 {
            let p = Int(floor(Double(idx) / Double(config.sprintsPerPI)))
            pi = config.firstPI + p
            inPI = idx - p * config.sprintsPerPI + 1
        }
        return SprintInfo(number: config.firstNumber + idx, start: start, end: end,
                          firstWeek: cal.weekRef(for: start), lastWeek: cal.weekRef(for: lastDay),
                          workday: done, workdaysTotal: total, pi: pi, sprintInPI: inPI,
                          sprintsPerPI: config.sprintsPerPI)
    }

    /// True if a sprint starts in this week (used to draw boundaries in the month grid).
    public static func startsSprint(_ ref: WeekRef, config: SprintConfig, cal: Calendar) -> Bool {
        guard config.enabled, let s = cal.startOfWeek(ref), let idx = index(for: s, config: config, cal: cal),
              let prev = cal.date(byAdding: .day, value: -1, to: s),
              let prevIdx = index(for: prev, config: config, cal: cal) else { return false }
        return idx != prevIdx
    }
}

// MARK: - Quarters

public enum QuarterMode: String, Codable, CaseIterable, Sendable {
    case months, weeks
    public var title: String { self == .months ? "By month" : "By week (13-week quarters)" }
}

public struct QuarterConfig: Codable, Equatable, Sendable {
    public var enabled: Bool = true
    public var fiscalStartMonth: Int = 1   // 1 = January (calendar year)
    public var mode: QuarterMode = .months
    public init() {}
}

public struct QuarterInfo: Equatable, Sendable {
    public let quarter: Int
    public let label: String     // "Q4 2026" or "Q3 FY27"
}

public enum Quarters {
    public static func info(for date: Date, config: QuarterConfig, cal: Calendar) -> QuarterInfo? {
        guard config.enabled else { return nil }
        if config.mode == .weeks {
            let ref = cal.weekRef(for: date)
            let q = min(4, (ref.week - 1) / 13 + 1)
            return QuarterInfo(quarter: q, label: "Q\(q) \(ref.year)")
        }
        let m = cal.component(.month, from: date), y = cal.component(.year, from: date)
        let start = min(max(config.fiscalStartMonth, 1), 12)
        let q = ((m - start + 12) % 12) / 3 + 1
        if start == 1 { return QuarterInfo(quarter: q, label: "Q\(q) \(y)") }
        let fy = m >= start ? y + 1 : y   // fiscal year named by its end year
        return QuarterInfo(quarter: q, label: "Q\(q) FY\(String(format: "%02d", fy % 100))")
    }
}

// MARK: - Milestones

public enum MilestoneColor: String, Codable, CaseIterable, Sendable {
    case orange, red, pink, purple, blue, teal, green
}

public struct Milestone: Codable, Identifiable, Equatable, Sendable {
    public var id: UUID
    public var name: String
    public var week: WeekRef
    public var color: MilestoneColor
    public init(id: UUID = UUID(), name: String, week: WeekRef, color: MilestoneColor = .orange) {
        self.id = id; self.name = name; self.week = week; self.color = color
    }
}

public struct MilestoneStatus: Equatable, Sendable {
    public let weeksAway: Int        // 0 = due this week, negative = past
    public let workdaysLeft: Int     // from today (inclusive) to the end of the milestone week
    public var isPast: Bool { weeksAway < 0 }
}

public enum Milestones {
    public static func status(_ m: Milestone, now: Date, cal: Calendar, holidays: HolidayStore) -> MilestoneStatus? {
        guard let weeks = cal.weeksBetween(cal.weekRef(for: now), m.week),
              let s = cal.startOfWeek(m.week), let end = cal.date(byAdding: .day, value: 7, to: s) else { return nil }
        let left = holidays.workingDays(from: cal.startOfDay(for: now), to: end, cal: cal)
        return MilestoneStatus(weeksAway: weeks, workdaysLeft: weeks < 0 ? 0 : left)
    }

    public static func upcoming(_ list: [Milestone], now: Date, cal: Calendar) -> [Milestone] {
        let cur = cal.weekRef(for: now)
        return list.filter { $0.week >= cur }.sorted { $0.week < $1.week }
    }
}
