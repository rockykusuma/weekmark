import Foundation

public struct QueryContext {
    public let cal: Calendar
    public let now: Date
    public let numbering: WeekNumbering
    public let holidays: HolidayStore
    public let milestones: [Milestone]
    public let sprint: SprintConfig

    public init(cal: Calendar, now: Date, numbering: WeekNumbering, holidays: HolidayStore = .empty,
                milestones: [Milestone] = [], sprint: SprintConfig = SprintConfig()) {
        self.cal = cal; self.now = now; self.numbering = numbering
        self.holidays = holidays; self.milestones = milestones; self.sprint = sprint
    }

    public var current: WeekRef { cal.weekRef(for: now) }
    var fmt: WeekFormatter { WeekFormatter(cal: cal) }
}

public struct QueryResult: Identifiable, Equatable {
    public enum Kind: String { case week, range, date, relative, countdown, milestone, holiday, hint, error }
    public let id = UUID()
    public let kind: Kind
    public let title: String
    public let subtitle: String
    public let detail: String?
    public let copyText: String?
    public let week: WeekRef?
    public let weeks: Int?
    public let workdays: Int?

    public init(kind: Kind, title: String, subtitle: String, detail: String? = nil, copyText: String? = nil,
                week: WeekRef? = nil, weeks: Int? = nil, workdays: Int? = nil) {
        self.kind = kind; self.title = title; self.subtitle = subtitle; self.detail = detail
        self.copyText = copyText; self.week = week; self.weeks = weeks; self.workdays = workdays
    }
}

public enum WeekParse: Equatable { case none, week(WeekRef), invalid(String) }

public enum QueryEngine {
    public static let examples = "Try 46 · CW3 2027 · Dec 12 · +6w · 46-50 · until CW52"

    // MARK: Entry point

    public static func evaluate(_ input: String, _ ctx: QueryContext) -> [QueryResult] {
        let q = input.trimmingCharacters(in: .whitespacesAndNewlines)
        let lower = q.lowercased()
        if q.isEmpty { return overview(ctx) }

        if let r = countdown(lower, ctx) { return [r] }
        if let r = keyword(lower, ctx) { return [r] + milestoneMatches(lower, ctx) }
        if let r = relative(lower, ctx) { return [r] }
        if let r = range(lower, ctx) { return [r] }
        switch parseWeek(lower, ctx) {
        case .week(let ref): return [weekResult(ref, ctx)]
        case .invalid(let msg): return [QueryResult(kind: .error, title: msg, subtitle: examples)]
        case .none: break
        }
        let ms = milestoneMatches(lower, ctx)
        if let d = detectDate(q, ctx) { return [dateResult(d, ctx)] + ms }
        if !ms.isEmpty { return ms }
        return [QueryResult(kind: .error, title: "No match for “\(q)”", subtitle: examples)]
    }

    /// Shown when the query is empty: this week, next milestones and holiday, and a hint.
    public static func overview(_ ctx: QueryContext) -> [QueryResult] {
        var out = [weekResult(ctx.current, ctx)]
        for m in Milestones.upcoming(ctx.milestones, now: ctx.now, cal: ctx.cal).prefix(3) {
            out.append(milestoneResult(m, ctx))
        }
        if let h = ctx.holidays.upcoming(from: ctx.now, cal: ctx.cal, limit: 1).first, let d = h.day.date(in: ctx.cal) {
            let ref = ctx.cal.weekRef(for: d)
            let weeks = ctx.cal.weeksBetween(ctx.current, ref) ?? 0
            out.append(QueryResult(kind: .holiday, title: "Next holiday: \(h.name)",
                                   subtitle: "\(ctx.fmt.format(d, "EEEEdMMMM")) · CW\(ref.week) · \(ctx.fmt.relative(weeks: weeks))",
                                   copyText: ctx.fmt.copyText(ref, currentYear: ctx.current.year), week: ref))
        }
        out.append(QueryResult(kind: .hint, title: "Type a week, date, range or offset", subtitle: examples))
        return out
    }

    // MARK: Week tokens

    /// Accepts "46", "CW46", "KW 46", "w46", "week 46", "46 2027", "46/2027", "2027-W46", "2027 46".
    public static func parseWeek(_ input: String, _ ctx: QueryContext) -> WeekParse {
        var s = input.lowercased().trimmingCharacters(in: .whitespaces)
        if s.hasPrefix("+") || s.hasPrefix("-") { return .none }
        for p in ["calendar week", "week", "cw", "kw", "wk"] { s = s.replacingOccurrences(of: p, with: "w") }
        let letters = s.filter(\.isLetter)
        guard letters.isEmpty || letters == "w" else { return .none }
        let nums = s.split(whereSeparator: { !$0.isNumber }).compactMap { Int($0) }
        var week: Int?, year: Int?
        switch nums.count {
        case 1:
            guard nums[0] < 100 else { return .none }
            week = nums[0]
        case 2:
            let big = nums.filter { $0 >= 1000 }, small = nums.filter { $0 < 100 }
            guard big.count == 1, small.count == 1 else { return .none }
            year = big[0]; week = small[0]
        default:
            return .none
        }
        guard let w = week else { return .none }
        var y = year ?? ctx.current.year
        // Late in the year, a small week number almost always means next year.
        if year == nil && ctx.current.week >= 40 && w <= 12 { y += 1 }
        let maxW = ctx.cal.weeksIn(weekYear: y)
        guard (1...maxW).contains(w) else { return .invalid("\(y) has only \(maxW) weeks") }
        return .week(WeekRef(year: y, week: w))
    }

    // MARK: Result builders

    public static func weekResult(_ ref: WeekRef, _ ctx: QueryContext) -> QueryResult {
        let f = ctx.fmt
        let weeks = ctx.cal.weeksBetween(ctx.current, ref) ?? 0
        let wd = ctx.holidays.workingDays(in: ref, cal: ctx.cal)
        var detail: [String] = []
        for h in ctx.holidays.holidays(in: ref, cal: ctx.cal) {
            if let d = h.day.date(in: ctx.cal) { detail.append("\(h.name) (\(f.format(d, "EEE")))") }
        }
        for m in ctx.milestones where m.week == ref { detail.append("◆ \(m.name)") }
        if let s = sprintLabel(ref, ctx) { detail.append(s) }
        if let other = otherNumbering(ref, ctx) { detail.append(other) }
        return QueryResult(kind: .week,
                           title: "CW\(ref.week) · \(f.longRange(ref))",
                           subtitle: "\(f.relative(weeks: weeks)) · \(f.months(ref)) · \(wd) working day\(wd == 1 ? "" : "s")",
                           detail: detail.isEmpty ? nil : detail.joined(separator: " · "),
                           copyText: f.copyText(ref, currentYear: ctx.current.year),
                           week: ref, weeks: weeks, workdays: wd)
    }

    static func milestoneResult(_ m: Milestone, _ ctx: QueryContext) -> QueryResult {
        let st = Milestones.status(m, now: ctx.now, cal: ctx.cal, holidays: ctx.holidays)
        let rel = ctx.fmt.relative(weeks: st?.weeksAway ?? 0)
        let left = st.map { $0.isPast ? "done" : "\($0.workdaysLeft) working days left" } ?? ""
        return QueryResult(kind: .milestone, title: "\(m.name) · CW\(m.week.week)",
                           subtitle: "\(rel) · \(left) · \(ctx.fmt.shortRange(m.week))",
                           copyText: "\(m.name): \(ctx.fmt.copyText(m.week, currentYear: ctx.current.year))",
                           week: m.week, weeks: st?.weeksAway, workdays: st?.workdaysLeft)
    }

    static func milestoneMatches(_ lower: String, _ ctx: QueryContext) -> [QueryResult] {
        guard lower.count >= 2 else { return [] }
        return ctx.milestones.filter { $0.name.lowercased().contains(lower) }.map { milestoneResult($0, ctx) }
    }

    static func sprintLabel(_ ref: WeekRef, _ ctx: QueryContext) -> String? {
        guard ctx.sprint.enabled, let s = ctx.cal.startOfWeek(ref),
              let info = Sprints.info(for: s, config: ctx.sprint, cal: ctx.cal, holidays: ctx.holidays) else { return nil }
        var label = "\(ctx.sprint.name) \(info.number)"
        if let pi = info.pi, let n = info.sprintInPI { label += " (PI \(pi), \(n)/\(info.sprintsPerPI))" }
        return label
    }

    /// Warns when ISO and US numbering disagree for this week — a classic EU/US mix-up.
    static func otherNumbering(_ ref: WeekRef, _ ctx: QueryContext) -> String? {
        guard ctx.numbering != .system, let days = Optional(ctx.cal.days(of: ref)), days.count == 7 else { return nil }
        let other: WeekNumbering = ctx.numbering == .iso ? .us : .iso
        let oc = makeCalendar(other, locale: ctx.cal.locale ?? .current, timeZone: ctx.cal.timeZone)
        // Compare on the Wednesday, which both systems place in the same week.
        let mid = days.first { ctx.cal.component(.weekday, from: $0) == 4 } ?? days[3]
        let ow = oc.component(.weekOfYear, from: mid)
        guard ow != ref.week else { return nil }
        return "\(other == .us ? "US" : "ISO") numbering: CW\(ow)"
    }

    static func dateResult(_ date: Date, _ ctx: QueryContext) -> QueryResult {
        let ref = ctx.cal.weekRef(for: date)
        let base = weekResult(ref, ctx)
        let hol = ctx.holidays.holidays(on: date, cal: ctx.cal).map(\.name)
        let detail = ([hol.isEmpty ? nil : "Holiday: " + hol.joined(separator: ", "), base.detail].compactMap { $0 }).joined(separator: " · ")
        return QueryResult(kind: .date,
                           title: "\(ctx.fmt.format(date, "EEEEdMMMMyyyy")) is in CW\(ref.week)",
                           subtitle: "\(ctx.fmt.shortRange(ref)) · \(ctx.fmt.relative(weeks: base.weeks ?? 0))",
                           detail: detail.isEmpty ? nil : detail,
                           copyText: base.copyText, week: ref, weeks: base.weeks, workdays: base.workdays)
    }

    // MARK: Keywords / relative / range / countdown

    static func keyword(_ lower: String, _ ctx: QueryContext) -> QueryResult? {
        let map: [String: Int] = ["today": 0, "now": 0, "this week": 0, "current": 0,
                                  "next week": 1, "last week": -1, "previous week": -1]
        guard let off = map[lower], let ref = ctx.cal.weekRef(ctx.current, adding: off) else { return nil }
        return weekResult(ref, ctx)
    }

    /// "+6", "+6w", "-2 weeks", "+10d", "in 6 weeks", "3 weeks ago", "6 weeks".
    static func relative(_ lower: String, _ ctx: QueryContext) -> QueryResult? {
        var s = lower.replacingOccurrences(of: " ", with: "")
        var sign = 0
        if s.hasPrefix("in") { s.removeFirst(2); sign = 1 }
        if s.hasSuffix("ago") { s.removeLast(3); sign = -1 }
        if s.hasPrefix("+") { s.removeFirst(); sign = 1 } else if s.hasPrefix("-") { s.removeFirst(); sign = -1 }
        let digits = s.prefix(while: \.isNumber)
        guard let n = Int(digits), n <= 520 else { return nil }
        let unit = String(s.dropFirst(digits.count))
        let isDays = ["d", "day", "days"].contains(unit)
        let isWeeks = ["", "w", "wk", "wks", "week", "weeks"].contains(unit)
        guard isDays || isWeeks else { return nil }
        if sign == 0 { if unit.isEmpty { return nil }; sign = 1 } // bare "6" is a week number, not an offset
        let amount = sign * n
        let f = ctx.fmt
        if isDays {
            guard let d = ctx.cal.date(byAdding: .day, value: amount, to: ctx.now) else { return nil }
            let ref = ctx.cal.weekRef(for: d)
            let wd = amount >= 0
                ? ctx.holidays.workingDays(from: ctx.cal.date(byAdding: .day, value: 1, to: ctx.cal.startOfDay(for: ctx.now))!, to: ctx.cal.date(byAdding: .day, value: 1, to: ctx.cal.startOfDay(for: d))!, cal: ctx.cal)
                : ctx.holidays.workingDays(from: ctx.cal.startOfDay(for: d), to: ctx.cal.startOfDay(for: ctx.now), cal: ctx.cal)
            return QueryResult(kind: .relative,
                               title: "\(amount >= 0 ? "+" : "−")\(n) day\(n == 1 ? "" : "s") → \(f.format(d, "EEEEdMMMMyyyy"))",
                               subtitle: "CW\(ref.week) · \(f.shortRange(ref)) · \(wd) working days \(amount >= 0 ? "away" : "ago")",
                               copyText: "\(f.format(d, "EEEdMMMyyyy")) (CW\(ref.week))", week: ref, workdays: wd)
        }
        guard let ref = ctx.cal.weekRef(ctx.current, adding: amount) else { return nil }
        let base = weekResult(ref, ctx)
        return QueryResult(kind: .relative,
                           title: "\(amount >= 0 ? "+" : "−")\(n) week\(n == 1 ? "" : "s") → CW\(ref.week)\(ref.year != ctx.current.year ? " \(ref.year)" : "")",
                           subtitle: "\(f.longRange(ref)) · \(base.workdays ?? 0) working days",
                           detail: base.detail, copyText: base.copyText, week: ref, weeks: amount, workdays: base.workdays)
    }

    /// "46-50", "CW46 – CW50", "46 to 2", "2026-W50..2027-W2".
    static func range(_ lower: String, _ ctx: QueryContext) -> QueryResult? {
        for sep in ["..", "–", "—", " to ", "-"] {
            let parts = lower.components(separatedBy: sep)
            guard parts.count == 2 else { continue }
            let l = parts[0].trimmingCharacters(in: .whitespaces), r = parts[1].trimmingCharacters(in: .whitespaces)
            guard !l.isEmpty, !r.isEmpty, case .week(let a) = parseWeek(l, ctx) else { continue }
            let b: WeekRef
            switch parseWeek(r, ctx) {
            case .week(let ref):
                let rightHasYear = r.split(whereSeparator: { !$0.isNumber }).contains { (Int($0) ?? 0) >= 1000 }
                if rightHasYear { b = ref } else {
                    // Inherit the left side's year; roll over if the range wraps into next year.
                    let y = ref.week < a.week ? a.year + 1 : a.year
                    guard ref.week <= ctx.cal.weeksIn(weekYear: y) else { return nil }
                    b = WeekRef(year: y, week: ref.week)
                }
            case .invalid(let m): return QueryResult(kind: .error, title: m, subtitle: examples)
            case .none: continue
            }
            return rangeResult(min(a, b), max(a, b), ctx)
        }
        return nil
    }

    public static func rangeResult(_ a: WeekRef, _ b: WeekRef, _ ctx: QueryContext) -> QueryResult? {
        guard let s = ctx.cal.startOfWeek(a), let bs = ctx.cal.startOfWeek(b),
              let e = ctx.cal.date(byAdding: .day, value: 7, to: bs),
              let last = ctx.cal.date(byAdding: .day, value: -1, to: e),
              let span = ctx.cal.weeksBetween(a, b) else { return nil }
        let weeks = span + 1
        let wd = ctx.holidays.workingDays(from: s, to: e, cal: ctx.cal)
        let f = ctx.fmt
        let holidays = Set((0..<weeks).compactMap { ctx.cal.weekRef(a, adding: $0) }.flatMap { ctx.holidays.holidays(in: $0, cal: ctx.cal).map(\.name) })
        let ya = a.year != ctx.current.year ? " \(a.year)" : "", yb = b.year != a.year ? " \(b.year)" : ""
        return QueryResult(kind: .range,
                           title: "CW\(a.week)\(ya) – CW\(b.week)\(yb) · \(weeks) week\(weeks == 1 ? "" : "s")",
                           subtitle: "\(f.longRange(s, last)) · \(wd) working days",
                           detail: holidays.isEmpty ? nil : "Includes \(holidays.count) holiday\(holidays.count == 1 ? "" : "s"): \(holidays.sorted().joined(separator: ", "))",
                           copyText: "CW\(a.week)\(ya)–CW\(b.week)\(yb) (\(f.shortInterval(s, last)))",
                           week: a, weeks: weeks, workdays: wd)
    }

    /// "until CW52", "to dec 24", "→ 50", "today -> CW3 2027".
    static func countdown(_ lower: String, _ ctx: QueryContext) -> QueryResult? {
        var s = lower
        for p in ["today", "now"] where s.hasPrefix(p) { s = String(s.dropFirst(p.count)) }
        s = s.trimmingCharacters(in: .whitespaces)
        var matched = false
        for p in ["until ", "till ", "til ", "to ", "→", "->", "=>"] where s.hasPrefix(p) {
            s = String(s.dropFirst(p.count)).trimmingCharacters(in: .whitespaces); matched = true; break
        }
        guard matched, !s.isEmpty else { return nil }
        let f = ctx.fmt
        let today = ctx.cal.startOfDay(for: ctx.now)
        if case .week(let ref) = parseWeek(s, ctx), let start = ctx.cal.startOfWeek(ref),
           let end = ctx.cal.date(byAdding: .day, value: 7, to: start) {
            let weeks = ctx.cal.weeksBetween(ctx.current, ref) ?? 0
            let toStart = ctx.holidays.workingDays(from: today, to: start, cal: ctx.cal)
            let toEnd = ctx.holidays.workingDays(from: today, to: end, cal: ctx.cal)
            return QueryResult(kind: .countdown,
                               title: "\(weeks) week\(weeks == 1 ? "" : "s") until CW\(ref.week)",
                               subtitle: "\(toStart) working days until it starts · \(toEnd) through its end · \(f.shortRange(ref))",
                               copyText: "\(weeks) weeks / \(toEnd) working days until end of CW\(ref.week)",
                               week: ref, weeks: weeks, workdays: toEnd)
        }
        if let d = detectDate(s, ctx) {
            let target = ctx.cal.startOfDay(for: d)
            let days = ctx.cal.dateComponents([.day], from: today, to: target).day ?? 0
            let wd = ctx.holidays.workingDays(from: today, to: target, cal: ctx.cal)
            let ref = ctx.cal.weekRef(for: d)
            return QueryResult(kind: .countdown,
                               title: "\(days) day\(days == 1 ? "" : "s") until \(f.format(d, "EEEdMMMyyyy"))",
                               subtitle: "\(wd) working days · CW\(ref.week) · \(f.relative(weeks: ctx.cal.weeksBetween(ctx.current, ref) ?? 0))",
                               copyText: "\(days) days / \(wd) working days until \(f.format(d, "dMMM"))",
                               week: ref, workdays: wd)
        }
        return nil
    }

    // MARK: Dates

    static func detectDate(_ s: String, _ ctx: QueryContext) -> Date? {
        guard let det = try? NSDataDetector(types: NSTextCheckingResult.CheckingType.date.rawValue) else { return nil }
        let ns = s as NSString
        guard let m = det.firstMatch(in: s, options: [], range: NSRange(location: 0, length: ns.length)),
              let d = m.date, m.range.length * 10 >= ns.length * 6 else { return nil }
        return d
    }
}
