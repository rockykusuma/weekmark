import XCTest
@testable import CWCore

/// Generates Tests/vectors/cwcore-v1.json: the expected answers the Windows (C#) port tests against.
/// Regenerate with: UPDATE_VECTORS=1 swift test --filter VectorExportTests
final class VectorExportTests: XCTestCase {
    let utc = TimeZone(identifier: "UTC")!
    let gb = Locale(identifier: "en_GB")
    let usL = Locale(identifier: "en_US")

    // MARK: helpers

    func cal(_ n: WeekNumbering, _ l: Locale? = nil) -> Calendar { makeCalendar(n, locale: l ?? gb, timeZone: utc) }
    func ds(_ d: Day) -> String { String(format: "%04d-%02d-%02d", d.y, d.m, d.d) }
    func ds(_ d: Date, _ c: Calendar) -> String { ds(Day(d, calendar: c)) }
    func at(_ d: Day, _ c: Calendar, hour: Int = 12) -> Date {
        c.date(from: DateComponents(year: d.y, month: d.m, day: d.d, hour: hour))!
    }
    func days(_ a: Day, _ b: Day) -> [Day] {
        var out: [Day] = [], d = a
        while d <= b { out.append(d); d = HolidayRules.add(d, 1) }
        return out
    }
    func j<T: Encodable>(_ v: T) -> Any {
        try! JSONSerialization.jsonObject(with: JSONEncoder().encode(v), options: [.fragmentsAllowed])
    }
    func store(_ regions: [String]) -> HolidayStore {
        HolidayStore(regions: Set(regions.map { HolidayRegion(rawValue: $0)! }))
    }
    func opt(_ v: Any?) -> Any { v ?? NSNull() }
    func wr(_ r: WeekRef) -> [String: Any] { ["year": r.year, "week": r.week] }

    // MARK: sections

    func weeks() -> [String: Any] {
        var set = Set(days(Day(2024, 1, 1), Day(2028, 12, 31)))
        for y in 2015...2040 { set.formUnion(days(Day(y, 12, 15), Day(y + 1, 1, 15))) }
        let sorted = set.sorted()
        func list(_ n: WeekNumbering) -> [[Any]] {
            let c = cal(n)
            return sorted.map { d in let r = c.weekRef(for: at(d, c)); return [ds(d), r.year, r.week] }
        }
        return ["iso": list(.iso), "us": list(.us)]
    }

    func weeksIn() -> [String: Any] {
        func m(_ n: WeekNumbering) -> [String: Int] {
            let c = cal(n)
            return Dictionary(uniqueKeysWithValues: (2000...2050).map { (String($0), c.weeksIn(weekYear: $0)) })
        }
        return ["iso": m(.iso), "us": m(.us)]
    }

    func holidays() -> [String: Any] {
        var out: [String: Any] = [:]
        for r in HolidayRegion.allCases {
            var years: [String: Any] = [:]
            for y in 2020...2035 { years[String(y)] = HolidayRules.holidays(r, year: y).map { [ds($0.day), $0.name] } }
            out[r.rawValue] = years
        }
        return out
    }

    func workingDays() -> [[String: Any]] {
        let c = cal(.iso)
        let cases: [([String], Day, Day)] = [
            ([], Day(2026, 12, 21), Day(2027, 1, 4)), (["de"], Day(2026, 12, 21), Day(2027, 1, 4)),
            (["gb"], Day(2026, 12, 21), Day(2027, 1, 4)), (["us"], Day(2026, 12, 21), Day(2027, 1, 5)),
            (["de"], Day(2026, 4, 1), Day(2026, 4, 8)), (["gb"], Day(2026, 4, 1), Day(2026, 4, 8)),
            (["dk"], Day(2026, 4, 1), Day(2026, 4, 8)), (["fr"], Day(2026, 5, 1), Day(2026, 6, 1)),
            (["de", "fr"], Day(2026, 5, 1), Day(2026, 6, 1)), (["pl"], Day(2026, 6, 1), Day(2026, 6, 12)),
            (["se"], Day(2026, 6, 15), Day(2026, 6, 30)), (["no"], Day(2026, 5, 10), Day(2026, 5, 25)),
            (["nl"], Day(2025, 4, 20), Day(2025, 5, 10)), (["in"], Day(2026, 1, 20), Day(2026, 1, 30)),
            (["us"], Day(2026, 11, 23), Day(2026, 11, 30)), (["gb"], Day(2026, 12, 24), Day(2027, 1, 4)),
            (["de"], Day(2025, 12, 29), Day(2026, 1, 5)), ([], Day(2026, 1, 1), Day(2027, 1, 1)),
            (["de"], Day(2026, 1, 1), Day(2027, 1, 1)), (["gb", "us"], Day(2028, 12, 20), Day(2029, 1, 3)),
            ([], Day(2026, 3, 3), Day(2026, 3, 3)), (["de"], Day(2026, 3, 10), Day(2026, 3, 3)),
            (["dk"], Day(2023, 5, 1), Day(2023, 5, 31)), (["us"], Day(2027, 7, 1), Day(2027, 7, 8)),
            ([], Day(2028, 2, 27), Day(2028, 3, 2)), (["de"], Day(2027, 3, 24), Day(2027, 4, 1)),
            (["gb"], Day(2024, 12, 23), Day(2025, 1, 3)), (["se"], Day(2026, 12, 21), Day(2027, 1, 7)),
        ]
        return cases.map { r, a, b in
            ["regions": r, "from": ds(a), "to": ds(b),
             "n": store(r).workingDays(from: at(a, c, hour: 0), to: at(b, c, hour: 0), cal: c)]
        }
    }

    func ics() -> [[String: Any]] {
        func ev(_ lines: String...) -> String {
            "BEGIN:VCALENDAR\nVERSION:2.0\nBEGIN:VEVENT\n" + lines.joined(separator: "\n") + "\nEND:VEVENT\nEND:VCALENDAR\n"
        }
        let samples: [(String, String)] = [
            ("basic all-day", ev("DTSTART;VALUE=DATE:20261225", "DTEND;VALUE=DATE:20261226", "SUMMARY:Christmas")),
            ("CRLF folded summary with escaped comma",
             ev("DTSTART;VALUE=DATE:20260504", "SUMMARY:Spring\\, Bank Holiday that is very long and the\n  summary is folded").replacingOccurrences(of: "\n", with: "\r\n")),
            ("timed with TZID", ev("DTSTART;TZID=Europe/Berlin:20261224T090000", "DTEND;TZID=Europe/Berlin:20261224T170000", "SUMMARY:Christmas Eve")),
            ("multi-day VALUE=DATE", ev("DTSTART;VALUE=DATE:20260810", "DTEND;VALUE=DATE:20260813", "SUMMARY:Company Retreat")),
            ("missing DTEND", ev("DTSTART;VALUE=DATE:20260701", "SUMMARY:Founders Day")),
            ("60-day event capped at 31", ev("DTSTART;VALUE=DATE:20260101", "DTEND;VALUE=DATE:20260302", "SUMMARY:Long Closure")),
            ("leading UTF-8 BOM", "\u{FEFF}" + ev("DTSTART;VALUE=DATE:20260317", "SUMMARY:St Patrick's Day")),
            ("no SUMMARY", ev("DTSTART;VALUE=DATE:20260909", "DTEND;VALUE=DATE:20260910")),
            ("DTEND equal to DTSTART", ev("DTSTART;VALUE=DATE:20260612", "DTEND;VALUE=DATE:20260612", "SUMMARY:Zero Length")),
            ("impossible DTSTART rolls over", ev("DTSTART;VALUE=DATE:20260230", "DTEND;VALUE=DATE:20260303", "SUMMARY:Feb 30")),
            ("month 13 single day", ev("DTSTART;VALUE=DATE:20261301", "SUMMARY:Month 13")),
            ("two events and escaped semicolon",
             ev("DTSTART;VALUE=DATE:20260102", "SUMMARY:A\\;B") + ev("DTSTART:20260103T000000Z", "SUMMARY:Second")),
        ]
        return samples.map { name, text in
            ["name": name, "text": text, "source": "Company",
             "holidays": ICSParser.parse(text, source: "Company").map { [ds($0.day), $0.name, $0.source] }]
        }
    }

    func sprintCfgs() -> [SprintConfig] {
        let off = SprintConfig()
        var plain = SprintConfig(); plain.enabled = true; plain.anchor = Day(2026, 1, 5)
        var pi = SprintConfig(); pi.enabled = true; pi.anchor = Day(2026, 1, 7); pi.firstNumber = 20
        pi.sprintsPerPI = 5; pi.firstPI = 3
        var three = SprintConfig(); three.enabled = true; three.lengthWeeks = 3; three.anchor = Day(2025, 12, 31)
        three.name = "Iteration"; three.sprintsPerPI = 4
        var one = SprintConfig(); one.enabled = true; one.lengthWeeks = 1; one.anchor = Day(2026, 6, 1)
        return [off, plain, pi, three, one]
    }

    func sprints() -> [[String: Any]] {
        let dates = [Day(2025, 12, 15), Day(2026, 1, 4), Day(2026, 1, 5), Day(2026, 3, 20), Day(2026, 6, 14),
                     Day(2026, 9, 30), Day(2026, 12, 24), Day(2026, 12, 31), Day(2027, 1, 1), Day(2027, 2, 15)]
        var out: [[String: Any]] = []
        for cfg in sprintCfgs() {
            for n in [WeekNumbering.iso, .us] {
                let c = cal(n)
                for regions in [[String](), ["de", "gb"]] {
                    let hs = store(regions)
                    for d in dates {
                        let date = at(d, c)
                        var info: Any = NSNull()
                        if let i = Sprints.info(for: date, config: cfg, cal: c, holidays: hs) {
                            info = ["number": i.number, "start": ds(i.start, c), "end": ds(i.end, c),
                                    "firstWeek": wr(i.firstWeek), "lastWeek": wr(i.lastWeek),
                                    "workday": i.workday, "workdaysTotal": i.workdaysTotal,
                                    "pi": opt(i.pi), "sprintInPI": opt(i.sprintInPI), "sprintsPerPI": i.sprintsPerPI] as [String: Any]
                        }
                        let cur = c.weekRef(for: date)
                        let starts: [[Any]] = (0..<12).map { k in
                            let r = c.weekRef(cur, adding: k)!
                            return [r.year, r.week, Sprints.startsSprint(r, config: cfg, cal: c)]
                        }
                        out.append(["config": j(cfg), "numbering": n.rawValue, "regions": regions, "date": ds(d),
                                    "info": info, "startsSprint": starts])
                    }
                }
            }
        }
        return out
    }

    func quarters() -> [[String: Any]] {
        var cfgs: [QuarterConfig] = []
        for s in [1, 4, 7, 10] { var q = QuarterConfig(); q.fiscalStartMonth = s; cfgs.append(q) }
        var w = QuarterConfig(); w.mode = .weeks; cfgs.append(w)
        var w2 = QuarterConfig(); w2.mode = .weeks; w2.fiscalStartMonth = 4; cfgs.append(w2)
        var off = QuarterConfig(); off.enabled = false; cfgs.append(off)
        let dates = [Day(2024, 2, 29), Day(2025, 12, 31), Day(2026, 1, 1), Day(2026, 3, 31), Day(2026, 4, 1),
                     Day(2026, 6, 30), Day(2026, 7, 1), Day(2026, 9, 30), Day(2026, 10, 1), Day(2026, 12, 31),
                     Day(2027, 1, 1), Day(2027, 1, 3), Day(2027, 1, 4), Day(2028, 12, 31)]
        var out: [[String: Any]] = []
        for cfg in cfgs {
            for n in [WeekNumbering.iso, .us] {
                let c = cal(n)
                for d in dates {
                    var info: Any = NSNull()
                    if let q = Quarters.info(for: at(d, c), config: cfg, cal: c) { info = ["quarter": q.quarter, "label": q.label] as [String: Any] }
                    out.append(["config": j(cfg), "numbering": n.rawValue, "date": ds(d), "info": info])
                }
            }
        }
        return out
    }

    func milestones() -> [[String: Any]] {
        let nows = [Day(2025, 12, 29), Day(2026, 1, 5), Day(2026, 3, 31), Day(2026, 7, 15), Day(2026, 10, 5),
                    Day(2026, 11, 10), Day(2026, 12, 28), Day(2026, 12, 31), Day(2027, 1, 1), Day(2028, 2, 29)]
        let regionSets: [[String]] = [[], ["de"], ["gb", "us"]]
        var out: [[String: Any]] = []
        var i = 0
        for n in [WeekNumbering.iso, .us] {
            let c = cal(n)
            for d in nows {
                let now = at(d, c), cur = c.weekRef(for: now)
                for off in [-5, -1, 0, 1, 4, 20, 60] {
                    let regions = regionSets[i % 3]; i += 1
                    let ref = c.weekRef(cur, adding: off)!
                    let m = Milestone(id: UUID(), name: "M", week: ref)
                    guard let st = Milestones.status(m, now: now, cal: c, holidays: store(regions)) else { continue }
                    out.append(["now": ds(d) + "T12:00", "numbering": n.rawValue, "regions": regions, "week": wr(ref),
                                "status": ["weeksAway": st.weeksAway, "workdaysLeft": st.workdaysLeft]])
                }
            }
        }
        return out
    }

    func format() -> [String: Any] {
        var result: [String: Any] = [:]
        let templates = ["EEE", "EEEdMMM", "EEEdMMMyyyy", "dMMM", "MMM", "MMMM", "MMMMyyyy", "EEEEMMMMd", "EEEEdMMMM", "EEEEdMMMMyyyy"]
        let tplDays = days(Day(2024, 2, 25), Day(2024, 3, 5)) + days(Day(2026, 1, 1), Day(2026, 12, 31))
            + days(Day(2027, 1, 1), Day(2027, 1, 10))
        for (tag, loc) in [("en-GB", gb), ("en-US", usL)] {
            let c = cal(.iso, loc), f = WeekFormatter(cal: c)
            var refs: [WeekRef] = []
            for y in 2025...2028 { refs += (1...c.weeksIn(weekYear: y)).map { WeekRef(year: y, week: $0) } }
            var copy: [[Any]] = []
            for r in refs { for cy in [r.year, r.year - 1] { copy.append([r.year, r.week, cy, f.copyText(r, currentYear: cy)]) } }
            var tpl: [String: Any] = [:]
            for t in templates { tpl[t] = tplDays.map { [ds($0), f.format(at($0, c), t)] } }
            result[tag] = [
                "shortRange": refs.map { [$0.year, $0.week, f.shortRange($0)] },
                "longRange": refs.map { [$0.year, $0.week, f.longRange($0)] },
                "months": refs.map { [$0.year, $0.week, f.months($0)] },
                "copyText": copy, "templates": tpl,
            ] as [String: Any]
        }
        return result
    }

    func weekDetails() -> [[String: Any]] {
        func ms(_ items: [(Int, String, Day, MilestoneColor)], _ c: Calendar) -> [Milestone] {
            items.map { Milestone(id: UUID(uuidString: String(format: "00000000-0000-0000-0000-%012d", $0.0))!, name: $0.1,
                                   week: c.weekRef(for: at($0.2, c)), color: $0.3) }
        }
        var plain = SprintConfig(); plain.enabled = true; plain.anchor = Day(2026, 1, 5)
        var pi = SprintConfig(); pi.enabled = true; pi.anchor = Day(2026, 1, 5); pi.sprintsPerPI = 5; pi.firstPI = 3
        var iter = SprintConfig(); iter.enabled = true; iter.lengthWeeks = 3; iter.name = "Iteration"; iter.anchor = Day(2025, 12, 29)
        let off = SprintConfig()
        typealias M = [(Int, String, Day, MilestoneColor)]
        let base: [(Day, [String], M, SprintConfig, Day)] = [
            (Day(2026, 11, 10), [], [], off, Day(2026, 11, 10)),
            (Day(2026, 11, 10), ["de"], [], off, Day(2026, 12, 24)),
            (Day(2026, 12, 28), ["gb"], [], off, Day(2026, 12, 28)),
            (Day(2026, 4, 1), ["gb"], [], off, Day(2026, 4, 6)),
            (Day(2026, 11, 25), ["us"], [], off, Day(2026, 11, 26)),
            (Day(2026, 10, 26), [], [(1, "Release", Day(2026, 11, 9), .red)], pi, Day(2026, 11, 9)),
            (Day(2026, 12, 30), ["de"], [], off, Day(2027, 1, 1)),
            (Day(2027, 1, 1), ["dk"], [], off, Day(2027, 3, 25)),
            (Day(2026, 1, 5), [], [], pi, Day(2026, 1, 5)),
            (Day(2026, 10, 15), [], [], plain, Day(2027, 1, 4)),
            (Day(2024, 12, 30), ["gb"], [], off, Day(2025, 1, 1)),
            (Day(2026, 7, 4), ["us"], [], off, Day(2026, 7, 4)),
            (Day(2026, 9, 1), ["se"], [], off, Day(2026, 6, 19)),
            (Day(2026, 5, 1), ["de", "gb"], [(2, "Beta", Day(2026, 5, 1), .blue), (3, "Docs freeze", Day(2026, 5, 2), .teal)], plain, Day(2026, 5, 1)),
            (Day(2026, 11, 2), ["nl"], [], off, Day(2026, 4, 27)),
            (Day(2028, 2, 28), ["fr"], [], off, Day(2028, 2, 29)),
            (Day(2026, 12, 31), ["pl"], [], off, Day(2026, 12, 24)),
            (Day(2026, 11, 10), [], [], iter, Day(2026, 11, 30)),
            (Day(2026, 3, 15), ["in"], [], off, Day(2026, 1, 26)),
            (Day(2026, 10, 5), ["no"], [], off, Day(2026, 5, 17)),
            (Day(2026, 12, 28), ["de"], [(4, "Year end", Day(2026, 12, 29), .green)], pi, Day(2026, 12, 29)),
            (Day(2026, 6, 10), ["se", "no"], [], off, Day(2026, 6, 22)),
        ]
        var out: [[String: Any]] = []
        for (loc, tag) in [(gb, "en-GB"), (usL, "en-US")] {
            for n in [WeekNumbering.iso, .us] {
                let c = cal(n, loc)
                for (nowD, regions, mlist, cfg, weekDay) in base {
                    let list = ms(mlist, c)
                    let ctx = QueryContext(cal: c, now: at(nowD, c), numbering: n, holidays: store(regions),
                                           milestones: list, sprint: cfg)
                    let ref = c.weekRef(for: at(weekDay, c))
                    let r = QueryEngine.weekResult(ref, ctx)
                    out.append(["locale": tag, "numbering": n.rawValue, "now": ds(nowD) + "T12:00", "regions": regions,
                                "milestones": list.map { j($0) }, "sprint": j(cfg), "week": wr(ref),
                                "title": r.title, "subtitle": r.subtitle, "detail": opt(r.detail), "copyText": opt(r.copyText)])
                }
            }
        }
        return out
    }

    /// Lookup answers. Only inputs that don't read the real clock: no year-less dates, no relative day words,
    /// numeric dates only when unambiguous (a component > 12).
    func queries() -> [[String: Any]] {
        let qs = ["", "46", "CW46", "KW 46", "w46", "week 46", "calendar week 46", "46 2027", "46/2027", "2027-W3",
                  "2027 3", "0", "54", "53", "99", "CW53 2027",
                  "today", "now", "this week", "current", "next week", "last week", "previous week",
                  "+6", "+6w", "-2w", "+10d", "-3d", "+1d", "in 6 weeks", "3 weeks ago", "6 weeks", "+600w",
                  "46-50", "46 - 50", "CW52\u{2013}CW2", "52-2", "46 to 48", "2026-W50..2027-W2", "50-46", "46-99",
                  "until CW52", "till 3 2027", "to 46", "\u{2192} 50", "-> 50", "today -> CW3 2027",
                  "until 24 December 2026", "to 2027-01-15",
                  "freeze", "release", "re", "x",
                  "24 Dec 2026", "Dec 24 2026", "December 24, 2026", "2026-12-24", "24/12/2026", "12/24/2026",
                  "1 January 2027", "Jan 4, 2027",
                  "hello", "\u{1F642}", "-", "to", "46-", "cw"]
        let nows = [Day(2026, 1, 6), Day(2026, 7, 15), Day(2026, 10, 7), Day(2026, 12, 29)]
        var plain = SprintConfig(); plain.enabled = true; plain.anchor = Day(2026, 1, 5)
        var pi = SprintConfig(); pi.enabled = true; pi.anchor = Day(2026, 1, 5); pi.sprintsPerPI = 5; pi.firstPI = 3
        let variants: [([String], Bool, SprintConfig)] = [([], false, SprintConfig()), (["de", "gb"], true, plain), (["us"], true, pi)]
        var out: [[String: Any]] = []
        var k = 0
        for q in qs {
            // ponytail: contexts are sampled, not the full cross product; every query hits both locales and numberings.
            for (loc, tag) in [(gb, "en-GB"), (usL, "en-US")] {
                for n in [WeekNumbering.iso, .us] {
                    for _ in 0..<1 {
                        let d = nows[k % 4], v = variants[(k / 4) % 3]; k += 1
                        let c = cal(n, loc), now = at(d, c), cur = c.weekRef(for: now)
                        let specs: [(Int, String, Int, MilestoneColor)] = [(1, "Code freeze", 3, .red), (2, "Release 2.0", 10, .blue), (3, "Hotfix", -2, .teal)]
                        let list: [Milestone] = !v.1 ? [] : specs.map { id, name, off, col in
                            Milestone(id: UUID(uuidString: String(format: "00000000-0000-0000-0000-%012d", id))!, name: name,
                                      week: c.weekRef(cur, adding: off)!, color: col)
                        }
                        let ctx = QueryContext(cal: c, now: now, numbering: n, holidays: store(v.0), milestones: list, sprint: v.2)
                        let rs = QueryEngine.evaluate(q, ctx).map { r -> [String: Any] in
                            ["kind": r.kind.rawValue, "title": r.title, "subtitle": r.subtitle, "detail": opt(r.detail),
                             "copyText": opt(r.copyText), "week": opt(r.week.map(wr)), "weeks": opt(r.weeks), "workdays": opt(r.workdays)]
                        }
                        out.append(["locale": tag, "numbering": n.rawValue, "now": ds(d) + "T12:00", "regions": v.0,
                                    "milestones": list.map { j($0) }, "sprint": j(v.2), "query": q, "results": rs])
                    }
                }
            }
        }
        return out
    }

    // MARK: test

    func build() -> [String: Any] {
        [
            "version": 1, "weeks": weeks(), "weeksIn": weeksIn(), "holidays": holidays(),
            "workingDays": workingDays(), "ics": ics(), "sprints": sprints(), "quarters": quarters(),
            "milestones": milestones(), "format": format(), "weekDetails": weekDetails(), "queries": queries(),
        ]
    }

    func testVectorsAreUpToDate() throws {
        let data = try JSONSerialization.data(withJSONObject: build(),
                                              options: [.sortedKeys, .prettyPrinted, .withoutEscapingSlashes]) + Data("\n".utf8)
        let url = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("vectors/cwcore-v1.json")
        if ProcessInfo.processInfo.environment["UPDATE_VECTORS"] == "1" {
            try data.write(to: url)
            return
        }
        let existing = try? Data(contentsOf: url)
        XCTAssertTrue(existing == data, "Vectors differ from \(url.path). Run UPDATE_VECTORS=1 swift test --filter VectorExportTests")
    }
}
