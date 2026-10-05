import Foundation

public struct MonthRow: Identifiable {
    public let id: Int
    public let ref: WeekRef
    public let days: [Date]
    public let isCurrent: Bool
}

/// Everything the widget needs to draw "now", computed once per tick.
public struct WeekSnapshot {
    public let cal: Calendar
    public let now: Date
    public let current: WeekRef
    public let weeksInYear: Int
    public let weekStart: Date
    public let days: [Date]
    public let weekProgress: Double
    public let fmt: WeekFormatter

    public var weekNumber: Int { current.week }
    public var weekYear: Int { current.year }

    public init(now: Date, numbering: WeekNumbering) {
        let cal = makeCalendar(numbering)
        self.cal = cal
        self.now = now
        fmt = WeekFormatter(cal: cal)
        current = cal.weekRef(for: now)
        let interval = cal.dateInterval(of: .weekOfYear, for: now)!
        weekStart = interval.start
        days = (0..<7).map { cal.date(byAdding: .day, value: $0, to: interval.start)! }
        weekProgress = min(1, max(0, now.timeIntervalSince(interval.start) / interval.duration))
        weeksInYear = cal.weeksIn(weekYear: current.year)
    }

    public var weeksLeft: Int { max(0, weeksInYear - weekNumber) }
    public var yearProgress: Double { Double(weekNumber) / Double(max(1, weeksInYear)) }
    public var rangeText: String { fmt.shortInterval(days[0], days[6]) }
    public var todayText: String { fmt.format(now, "EEEEMMMMd") }

    public func displayedMonth(offset: Int) -> Date {
        cal.date(byAdding: .month, value: offset, to: cal.startOfMonth(now))!
    }
    public func monthTitle(_ month: Date) -> String { fmt.format(month, "MMMMyyyy") }

    public func isToday(_ d: Date) -> Bool { cal.isDate(d, inSameDayAs: now) }
    public func isPast(_ d: Date) -> Bool { d < cal.startOfDay(for: now) }
    public func isWeekend(_ d: Date) -> Bool { !cal.isWorkday(d) }
    public func isIn(_ d: Date, month: Date) -> Bool { cal.isDate(d, equalTo: month, toGranularity: .month) }
    public func dayNumber(_ d: Date) -> String { String(cal.component(.day, from: d)) }

    public func weekdaySymbol(_ d: Date, veryShort: Bool = false) -> String {
        let i = cal.component(.weekday, from: d) - 1
        let syms = veryShort ? cal.veryShortStandaloneWeekdaySymbols : cal.shortStandaloneWeekdaySymbols
        return syms[i]
    }

    /// Always 6 rows so the widget height stays constant month to month.
    public func monthRows(month: Date) -> [MonthRow] {
        var start = cal.dateInterval(of: .weekOfYear, for: month)!.start
        var rows: [MonthRow] = []
        for i in 0..<6 {
            let days = (0..<7).map { cal.date(byAdding: .day, value: $0, to: start)! }
            rows.append(MonthRow(id: i, ref: cal.weekRef(for: start), days: days,
                                 isCurrent: cal.isDate(start, inSameDayAs: weekStart)))
            start = cal.date(byAdding: .weekOfYear, value: 1, to: start)!
        }
        return rows
    }

    /// Month offset (from the current month) of the month that "owns" a week — the month of its middle day.
    public func monthOffset(for ref: WeekRef) -> Int? {
        guard let start = cal.startOfWeek(ref), let mid = cal.date(byAdding: .day, value: 3, to: start) else { return nil }
        return cal.dateComponents([.month], from: cal.startOfMonth(now), to: cal.startOfMonth(mid)).month
    }
}
