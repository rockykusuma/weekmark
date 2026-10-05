import Foundation

public struct Holiday: Hashable, Codable, Sendable {
    public let day: Day
    public let name: String
    public let source: String   // region code, "custom" or an imported calendar name
    public init(day: Day, name: String, source: String) { self.day = day; self.name = name; self.source = source }
}

public enum HolidayRegion: String, CaseIterable, Codable, Sendable {
    case us, gb, de, dk, nl, fr, se, no, pl, `in`

    public var title: String {
        switch self {
        case .us: return "United States"
        case .gb: return "United Kingdom (England & Wales)"
        case .de: return "Germany (national)"
        case .dk: return "Denmark"
        case .nl: return "Netherlands"
        case .fr: return "France"
        case .se: return "Sweden"
        case .no: return "Norway"
        case .pl: return "Poland"
        case .in: return "India (national; import your company list for festivals)"
        }
    }
    public var flag: String {
        switch self {
        case .us: return "🇺🇸"; case .gb: return "🇬🇧"; case .de: return "🇩🇪"; case .dk: return "🇩🇰"
        case .nl: return "🇳🇱"; case .fr: return "🇫🇷"; case .se: return "🇸🇪"; case .no: return "🇳🇴"
        case .pl: return "🇵🇱"; case .in: return "🇮🇳"
        }
    }

    /// Best guess from the user's locale.
    public static func guess(locale: Locale = .current) -> HolidayRegion? {
        guard let r = locale.region?.identifier.lowercased() else { return nil }
        if r == "uk" { return .gb }
        return HolidayRegion(rawValue: r)
    }
}

enum HolidayRules {
    static let greg: Calendar = {
        var c = Calendar(identifier: .gregorian)
        c.timeZone = TimeZone(identifier: "UTC")!
        return c
    }()

    /// Gregorian Easter Sunday (anonymous Gregorian algorithm).
    static func easter(_ y: Int) -> Day {
        let a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4
        let f = (b + 8) / 25, g = (b - f + 1) / 3
        let h = (19 * a + b - d - g + 15) % 30
        let i = c / 4, k = c % 4
        let l = (32 + 2 * e + 2 * i - h - k) % 7
        let m = (a + 11 * h + 22 * l) / 451
        let month = (h + l - 7 * m + 114) / 31
        let day = ((h + l - 7 * m + 114) % 31) + 1
        return Day(y, month, day)
    }

    static func date(_ d: Day) -> Date { greg.date(from: DateComponents(year: d.y, month: d.m, day: d.d))! }
    static func day(_ date: Date) -> Day {
        let c = greg.dateComponents([.year, .month, .day], from: date)
        return Day(c.year!, c.month!, c.day!)
    }
    static func add(_ d: Day, _ n: Int) -> Day { day(greg.date(byAdding: .day, value: n, to: date(d))!) }
    static func weekday(_ d: Day) -> Int { greg.component(.weekday, from: date(d)) } // 1 = Sunday

    /// n-th weekday (1=Sun…7=Sat) of a month; n = -1 for the last.
    static func nth(_ y: Int, _ m: Int, weekday: Int, _ n: Int) -> Day {
        if n > 0 {
            let first = Day(y, m, 1)
            let offset = (weekday - self.weekday(first) + 7) % 7
            return add(first, offset + (n - 1) * 7)
        }
        let nextMonth = m == 12 ? Day(y + 1, 1, 1) : Day(y, m + 1, 1)
        let last = add(nextMonth, -1)
        let offset = (self.weekday(last) - weekday + 7) % 7
        return add(last, -offset)
    }

    /// US federal "observed": Saturday → Friday, Sunday → Monday.
    static func usObserved(_ d: Day) -> Day {
        switch weekday(d) { case 7: return add(d, -1); case 1: return add(d, 1); default: return d }
    }

    /// UK substitute days: weekend → next Monday (Boxing Day shifts past Christmas substitute).
    static func ukSubstitute(_ d: Day, taken: Set<Day> = []) -> Day {
        var r = d
        while weekday(r) == 1 || weekday(r) == 7 || taken.contains(r) { r = add(r, 1) }
        return r
    }

    static func holidays(_ region: HolidayRegion, year y: Int) -> [Holiday] {
        let E = easter(y)
        var list: [(Day, String)] = []
        func a(_ d: Day, _ n: String) { list.append((d, n)) }
        switch region {
        case .us:
            a(usObserved(Day(y, 1, 1)), "New Year's Day")
            a(nth(y, 1, weekday: 2, 3), "Martin Luther King Jr. Day")
            a(nth(y, 2, weekday: 2, 3), "Presidents' Day")
            a(nth(y, 5, weekday: 2, -1), "Memorial Day")
            a(usObserved(Day(y, 6, 19)), "Juneteenth")
            a(usObserved(Day(y, 7, 4)), "Independence Day")
            a(nth(y, 9, weekday: 2, 1), "Labor Day")
            a(nth(y, 10, weekday: 2, 2), "Columbus Day")
            a(usObserved(Day(y, 11, 11)), "Veterans Day")
            a(nth(y, 11, weekday: 5, 4), "Thanksgiving")
            a(usObserved(Day(y, 12, 25)), "Christmas Day")
        case .gb:
            a(ukSubstitute(Day(y, 1, 1)), "New Year's Day")
            a(add(E, -2), "Good Friday")
            a(add(E, 1), "Easter Monday")
            a(nth(y, 5, weekday: 2, 1), "Early May Bank Holiday")
            a(nth(y, 5, weekday: 2, -1), "Spring Bank Holiday")
            a(nth(y, 8, weekday: 2, -1), "Summer Bank Holiday")
            let xmas = ukSubstitute(Day(y, 12, 25))
            a(xmas, "Christmas Day")
            a(ukSubstitute(Day(y, 12, 26), taken: [xmas]), "Boxing Day")
        case .de:
            a(Day(y, 1, 1), "Neujahr"); a(add(E, -2), "Karfreitag"); a(add(E, 1), "Ostermontag")
            a(Day(y, 5, 1), "Tag der Arbeit"); a(add(E, 39), "Christi Himmelfahrt"); a(add(E, 50), "Pfingstmontag")
            a(Day(y, 10, 3), "Tag der Deutschen Einheit"); a(Day(y, 12, 25), "1. Weihnachtstag"); a(Day(y, 12, 26), "2. Weihnachtstag")
        case .dk:
            a(Day(y, 1, 1), "Nytårsdag"); a(add(E, -3), "Skærtorsdag"); a(add(E, -2), "Langfredag"); a(add(E, 1), "2. påskedag")
            if y < 2024 { a(add(E, 26), "Store bededag") }
            a(add(E, 39), "Kristi himmelfartsdag"); a(add(E, 50), "2. pinsedag")
            a(Day(y, 6, 5), "Grundlovsdag"); a(Day(y, 12, 24), "Juleaftensdag")
            a(Day(y, 12, 25), "1. juledag"); a(Day(y, 12, 26), "2. juledag")
        case .nl:
            a(Day(y, 1, 1), "Nieuwjaarsdag"); a(add(E, 1), "Tweede Paasdag")
            let kd = Day(y, 4, 27); a(weekday(kd) == 1 ? Day(y, 4, 26) : kd, "Koningsdag")
            if y % 5 == 0 { a(Day(y, 5, 5), "Bevrijdingsdag") }
            a(add(E, 39), "Hemelvaartsdag"); a(add(E, 50), "Tweede Pinksterdag")
            a(Day(y, 12, 25), "Eerste Kerstdag"); a(Day(y, 12, 26), "Tweede Kerstdag")
        case .fr:
            a(Day(y, 1, 1), "Jour de l'an"); a(add(E, 1), "Lundi de Pâques"); a(Day(y, 5, 1), "Fête du Travail")
            a(Day(y, 5, 8), "Victoire 1945"); a(add(E, 39), "Ascension"); a(add(E, 50), "Lundi de Pentecôte")
            a(Day(y, 7, 14), "Fête nationale"); a(Day(y, 8, 15), "Assomption"); a(Day(y, 11, 1), "Toussaint")
            a(Day(y, 11, 11), "Armistice"); a(Day(y, 12, 25), "Noël")
        case .se:
            a(Day(y, 1, 1), "Nyårsdagen"); a(Day(y, 1, 6), "Trettondedag jul"); a(add(E, -2), "Långfredagen")
            a(add(E, 1), "Annandag påsk"); a(Day(y, 5, 1), "Första maj"); a(add(E, 39), "Kristi himmelsfärdsdag")
            a(Day(y, 6, 6), "Nationaldagen")
            // Midsummer Eve: Friday between 19–25 June (de facto day off)
            let jun19 = Day(y, 6, 19)
            a(add(jun19, (6 - weekday(jun19) + 7) % 7), "Midsommarafton")
            a(Day(y, 12, 24), "Julafton"); a(Day(y, 12, 25), "Juldagen"); a(Day(y, 12, 26), "Annandag jul")
            a(Day(y, 12, 31), "Nyårsafton")
        case .no:
            a(Day(y, 1, 1), "Første nyttårsdag"); a(add(E, -3), "Skjærtorsdag"); a(add(E, -2), "Langfredag")
            a(add(E, 1), "Andre påskedag"); a(Day(y, 5, 1), "Arbeidernes dag"); a(Day(y, 5, 17), "Grunnlovsdag")
            a(add(E, 39), "Kristi himmelfartsdag"); a(add(E, 50), "Andre pinsedag")
            a(Day(y, 12, 25), "Første juledag"); a(Day(y, 12, 26), "Andre juledag")
        case .pl:
            a(Day(y, 1, 1), "Nowy Rok"); a(Day(y, 1, 6), "Trzech Króli"); a(add(E, 1), "Poniedziałek Wielkanocny")
            a(Day(y, 5, 1), "Święto Pracy"); a(Day(y, 5, 3), "Święto Konstytucji"); a(add(E, 60), "Boże Ciało")
            a(Day(y, 8, 15), "Wniebowzięcie NMP"); a(Day(y, 11, 1), "Wszystkich Świętych")
            a(Day(y, 11, 11), "Święto Niepodległości")
            if y >= 2025 { a(Day(y, 12, 24), "Wigilia") }
            a(Day(y, 12, 25), "Boże Narodzenie"); a(Day(y, 12, 26), "Drugi dzień Bożego Narodzenia")
        case .in:
            a(Day(y, 1, 26), "Republic Day"); a(Day(y, 8, 15), "Independence Day"); a(Day(y, 10, 2), "Gandhi Jayanti")
        }
        return list.map { Holiday(day: $0.0, name: $0.1, source: region.rawValue) }
    }
}

/// Holidays for a set of regions plus custom/imported days, with per-year caching.
public final class HolidayStore {
    public let regions: Set<HolidayRegion>
    public let custom: [Holiday]
    private var cache: [Int: [Day: [Holiday]]] = [:]
    private let customByDay: [Day: [Holiday]]

    public init(regions: Set<HolidayRegion>, custom: [Holiday] = []) {
        self.regions = regions
        self.custom = custom
        customByDay = Dictionary(grouping: custom, by: \.day)
    }

    public static let empty = HolidayStore(regions: [])

    private func table(_ y: Int) -> [Day: [Holiday]] {
        if let t = cache[y] { return t }
        var t: [Day: [Holiday]] = [:]
        for r in regions.sorted(by: { $0.rawValue < $1.rawValue }) {
            for h in HolidayRules.holidays(r, year: y) { t[h.day, default: []].append(h) }
        }
        for (d, hs) in customByDay where d.y == y { t[d, default: []].append(contentsOf: hs) }
        cache[y] = t
        return t
    }

    public func holidays(on d: Day) -> [Holiday] { table(d.y)[d] ?? [] }
    public func isHoliday(_ d: Day) -> Bool { !holidays(on: d).isEmpty }

    public func holidays(on date: Date, cal: Calendar) -> [Holiday] { holidays(on: Day(date, calendar: cal)) }

    public func upcoming(from date: Date, cal: Calendar, limit: Int = 5) -> [Holiday] {
        let start = Day(date, calendar: cal)
        var out: [Holiday] = []
        for y in start.y...(start.y + 1) {
            let t = table(y)
            for d in t.keys.sorted() where d >= start {
                // collapse same-name duplicates across regions
                for h in t[d]! where !out.contains(where: { $0.day == h.day && $0.name == h.name }) { out.append(h) }
                if out.count >= limit { return Array(out.prefix(limit)) }
            }
        }
        return out
    }

    /// Working days in [start, end): weekdays per the calendar's weekend, minus holidays.
    public func workingDays(from start: Date, to end: Date, cal: Calendar) -> Int {
        guard end > start else { return 0 }
        var n = 0
        var d = cal.startOfDay(for: start)
        while d < end {
            if cal.isWorkday(d) && !isHoliday(Day(d, calendar: cal)) { n += 1 }
            d = cal.date(byAdding: .day, value: 1, to: d)!
        }
        return n
    }

    public func workingDays(in ref: WeekRef, cal: Calendar) -> Int {
        guard let s = cal.startOfWeek(ref), let e = cal.date(byAdding: .day, value: 7, to: s) else { return 0 }
        return workingDays(from: s, to: e, cal: cal)
    }

    public func holidays(in ref: WeekRef, cal: Calendar) -> [Holiday] {
        var seen = Set<String>()
        return cal.days(of: ref).flatMap { holidays(on: $0, cal: cal) }.filter { seen.insert("\($0.day.y)\($0.day.m)\($0.day.d)\($0.name)").inserted }
    }
}

public extension Calendar {
    /// Monday–Friday, regardless of locale weekend rules (software teams work Mon–Fri).
    func isWorkday(_ d: Date) -> Bool {
        let wd = component(.weekday, from: d)
        return wd != 1 && wd != 7
    }
}

/// Minimal iCalendar parser for holiday lists: all-day (and timed) VEVENTs → one Holiday per day.
public enum ICSParser {
    public static func parse(_ text: String, source: String) -> [Holiday] {
        // Unfold continuation lines.
        let raw = text.replacingOccurrences(of: "\r\n", with: "\n").replacingOccurrences(of: "\r", with: "\n")
        var lines: [String] = []
        for l in raw.components(separatedBy: "\n") {
            if (l.hasPrefix(" ") || l.hasPrefix("\t")), !lines.isEmpty { lines[lines.count - 1] += l.dropFirst() }
            else { lines.append(l) }
        }
        var out: [Holiday] = []
        var inEvent = false
        var start: Day?, end: Day?, summary = ""
        for line in lines {
            if line == "BEGIN:VEVENT" { inEvent = true; start = nil; end = nil; summary = ""; continue }
            if line == "END:VEVENT" {
                if inEvent, let s = start {
                    var d = s
                    let last = end.map { $0 > s ? HolidayRules.add($0, -1) : s } ?? s // DTEND is exclusive
                    var guardCount = 0
                    while d <= last && guardCount < 31 {
                        out.append(Holiday(day: d, name: summary.isEmpty ? "Holiday" : summary, source: source))
                        d = HolidayRules.add(d, 1); guardCount += 1
                    }
                }
                inEvent = false; continue
            }
            guard inEvent, let colon = line.firstIndex(of: ":") else { continue }
            let key = line[..<colon].split(separator: ";").first.map(String.init)?.uppercased() ?? ""
            let value = String(line[line.index(after: colon)...])
            switch key {
            case "DTSTART": start = parseDay(value)
            case "DTEND": end = parseDay(value)
            case "SUMMARY": summary = value.replacingOccurrences(of: "\\,", with: ",").replacingOccurrences(of: "\\;", with: ";")
            default: break
            }
        }
        return out
    }

    static func parseDay(_ v: String) -> Day? {
        let digits = v.prefix(8)
        guard digits.count == 8, let n = Int(digits) else { return nil }
        return Day(n / 10000, (n / 100) % 100, n % 100)
    }
}
