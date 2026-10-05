import Foundation

public enum WeekNumbering: String, CaseIterable, Codable, Sendable {
    case iso, us, system
    public var title: String {
        switch self {
        case .iso: return "ISO 8601 (Monday start)"
        case .us: return "US (Sunday start)"
        case .system: return "System default"
        }
    }
}

public func makeCalendar(_ n: WeekNumbering, locale: Locale = .current, timeZone: TimeZone = .current) -> Calendar {
    var c: Calendar
    switch n {
    case .iso: c = Calendar(identifier: .iso8601)
    case .us: c = Calendar(identifier: .gregorian)
    case .system: c = Calendar.current
    }
    c.locale = locale
    c.timeZone = timeZone
    // Set after locale, which can otherwise override these.
    switch n {
    case .iso: c.firstWeekday = 2; c.minimumDaysInFirstWeek = 4
    case .us: c.firstWeekday = 1; c.minimumDaysInFirstWeek = 1
    case .system: break
    }
    return c
}

/// A week identified by its week-numbering year and week number (e.g. 2026-W46).
public struct WeekRef: Equatable, Hashable, Codable, Comparable, Sendable {
    public let year: Int
    public let week: Int
    public init(year: Int, week: Int) { self.year = year; self.week = week }
    public static func < (a: WeekRef, b: WeekRef) -> Bool { (a.year, a.week) < (b.year, b.week) }
}

/// A calendar-independent civil date (Gregorian y/m/d), used for holidays.
public struct Day: Hashable, Comparable, Codable, Sendable {
    public let y: Int, m: Int, d: Int
    public init(_ y: Int, _ m: Int, _ d: Int) { self.y = y; self.m = m; self.d = d }
    public init(_ date: Date, calendar: Calendar) {
        let c = calendar.dateComponents(in: calendar.timeZone, from: date)
        // Always Gregorian civil date; ISO8601 and Gregorian agree on y/m/d.
        self.init(c.year!, c.month!, c.day!)
    }
    public static func < (a: Day, b: Day) -> Bool { (a.y, a.m, a.d) < (b.y, b.m, b.d) }
    public func date(in cal: Calendar) -> Date? { cal.date(from: DateComponents(year: y, month: m, day: d)) }
}

public extension Calendar {
    func startOfWeek(_ ref: WeekRef) -> Date? {
        var c = DateComponents()
        c.yearForWeekOfYear = ref.year
        c.weekOfYear = ref.week
        c.weekday = firstWeekday
        return date(from: c)
    }

    func weeksIn(weekYear y: Int) -> Int {
        guard let next = startOfWeek(WeekRef(year: y + 1, week: 1)),
              let last = date(byAdding: .day, value: -1, to: next) else { return 52 }
        return component(.weekOfYear, from: last)
    }

    func weekRef(for d: Date) -> WeekRef {
        WeekRef(year: component(.yearForWeekOfYear, from: d), week: component(.weekOfYear, from: d))
    }

    func startOfMonth(_ d: Date) -> Date { dateInterval(of: .month, for: d)!.start }

    func weekRef(_ ref: WeekRef, adding weeks: Int) -> WeekRef? {
        guard let s = startOfWeek(ref), let t = date(byAdding: .weekOfYear, value: weeks, to: s) else { return nil }
        return weekRef(for: t)
    }

    /// Whole weeks from week `a` to week `b` (b - a).
    func weeksBetween(_ a: WeekRef, _ b: WeekRef) -> Int? {
        guard let sa = startOfWeek(a), let sb = startOfWeek(b) else { return nil }
        let days = dateComponents([.day], from: sa, to: sb).day ?? 0
        return Int((Double(days) / 7).rounded())
    }

    func days(of ref: WeekRef) -> [Date] {
        guard let s = startOfWeek(ref) else { return [] }
        return (0..<7).compactMap { date(byAdding: .day, value: $0, to: s) }
    }
}

/// Formatting helpers bound to a calendar + locale.
public struct WeekFormatter {
    public let cal: Calendar
    public init(cal: Calendar) { self.cal = cal }

    public func format(_ d: Date, _ template: String) -> String {
        let f = DateFormatter(); f.calendar = cal; f.locale = cal.locale ?? .current; f.timeZone = cal.timeZone
        f.setLocalizedDateFormatFromTemplate(template)
        return f.string(from: d)
    }

    public func interval(_ a: Date, _ b: Date, _ template: String) -> String {
        let f = DateIntervalFormatter()
        f.calendar = cal; f.locale = cal.locale ?? .current; f.timeZone = cal.timeZone
        f.dateTemplate = template
        return f.string(from: a, to: b)
    }

    /// "Mon, 9 Nov – Sun, 15 Nov 2026"
    public func longRange(_ ref: WeekRef) -> String {
        let days = cal.days(of: ref)
        guard let s = days.first, let e = days.last else { return "" }
        return longRange(s, e)
    }

    public func longRange(_ s: Date, _ e: Date) -> String {
        let y1 = cal.component(.year, from: s), y2 = cal.component(.year, from: e)
        return y1 == y2
            ? "\(format(s, "EEEdMMM")) – \(format(e, "EEEdMMM")) \(y2)"
            : "\(format(s, "EEEdMMMyyyy")) – \(format(e, "EEEdMMMyyyy"))"
    }

    /// "9–15 Nov"
    public func shortRange(_ ref: WeekRef) -> String {
        let days = cal.days(of: ref)
        guard let s = days.first, let e = days.last else { return "" }
        return shortInterval(s, e)
    }

    /// Locale-ordered short range: "9–15 Nov" / "Nov 9–15", "30 Nov – 6 Dec".
    /// (DateIntervalFormatter doesn't reliably honour the locale for these templates.)
    public func shortInterval(_ s: Date, _ e: Date) -> String {
        let sameMonth = cal.isDate(s, equalTo: e, toGranularity: .month)
        guard sameMonth else { return "\(format(s, "dMMM")) – \(format(e, "dMMM"))" }
        let pattern = DateFormatter.dateFormat(fromTemplate: "dMMM", options: 0, locale: cal.locale ?? .current) ?? "d MMM"
        let dayFirst = (pattern.firstIndex(of: "d") ?? pattern.endIndex) < (pattern.firstIndex(of: "M") ?? pattern.endIndex)
        let d1 = cal.component(.day, from: s), d2 = cal.component(.day, from: e)
        let month = format(s, "MMM")
        return dayFirst ? "\(d1)–\(d2) \(month)" : "\(month) \(d1)–\(d2)"
    }

    public func relative(weeks diff: Int) -> String {
        switch diff {
        case 0: return "This week"
        case 1: return "Next week"
        case -1: return "Last week"
        case let n where n > 0: return "In \(n) weeks"
        default: return "\(-diff) weeks ago"
        }
    }

    public func months(_ ref: WeekRef) -> String {
        let days = cal.days(of: ref)
        guard let s = days.first, let e = days.last else { return "" }
        let m1 = format(s, "MMMM"), m2 = format(e, "MMMM")
        return m1 == m2 ? m1 : "\(m1) / \(m2)"
    }

    /// "CW46 (9–15 Nov)" — adds the year when it isn't the current week-year.
    public func copyText(_ ref: WeekRef, currentYear: Int) -> String {
        let y = ref.year == currentYear ? "" : " \(ref.year)"
        return "CW\(ref.week)\(y) (\(shortRange(ref)))"
    }
}
