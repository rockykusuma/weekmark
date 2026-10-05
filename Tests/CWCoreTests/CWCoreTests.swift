import XCTest
@testable import CWCore

class CalTestCase: XCTestCase {
    let locale = Locale(identifier: "en_GB")
    let tz = TimeZone(identifier: "Asia/Kolkata")!
    lazy var iso = makeCalendar(.iso, locale: locale, timeZone: tz)
    lazy var us = makeCalendar(.us, locale: locale, timeZone: tz)

    func date(_ y: Int, _ m: Int, _ d: Int, _ h: Int = 12) -> Date {
        iso.date(from: DateComponents(year: y, month: m, day: d, hour: h))!
    }
    func ctx(_ y: Int, _ m: Int, _ d: Int, numbering: WeekNumbering = .iso, holidays: HolidayStore = .empty,
             milestones: [Milestone] = [], sprint: SprintConfig = SprintConfig()) -> QueryContext {
        QueryContext(cal: numbering == .iso ? iso : us, now: date(y, m, d), numbering: numbering,
                     holidays: holidays, milestones: milestones, sprint: sprint)
    }

}

final class WeekMathTests: CalTestCase {
    func testISOWeekNumbers() {
        XCTAssertEqual(iso.weekRef(for: date(2026, 10, 5)), WeekRef(year: 2026, week: 41))
        // Jan 1 2027 (Friday) belongs to 2026-W53
        XCTAssertEqual(iso.weekRef(for: date(2027, 1, 1)), WeekRef(year: 2026, week: 53))
        // Dec 29 2025 (Monday) is 2026-W01
        XCTAssertEqual(iso.weekRef(for: date(2025, 12, 29)), WeekRef(year: 2026, week: 1))
        XCTAssertEqual(iso.weekRef(for: date(2027, 1, 4)), WeekRef(year: 2027, week: 1))
    }

    func testWeeksInYear() {
        XCTAssertEqual(iso.weeksIn(weekYear: 2026), 53)
        XCTAssertEqual(iso.weeksIn(weekYear: 2027), 52)
        XCTAssertEqual(iso.weeksIn(weekYear: 2020), 53)
        XCTAssertEqual(iso.weeksIn(weekYear: 2032), 53)
        XCTAssertEqual(iso.weeksIn(weekYear: 2025), 52)
    }

    func testStartOfWeekRoundTrip() {
        for y in 2024...2030 {
            for w in 1...iso.weeksIn(weekYear: y) {
                let ref = WeekRef(year: y, week: w)
                let s = iso.startOfWeek(ref)!
                XCTAssertEqual(iso.component(.weekday, from: s), 2, "\(ref) should start Monday")
                XCTAssertEqual(iso.weekRef(for: s), ref)
            }
        }
    }

    func testWeeksBetweenAcrossYearAndDST() {
        XCTAssertEqual(iso.weeksBetween(WeekRef(year: 2026, week: 41), WeekRef(year: 2026, week: 46)), 5)
        XCTAssertEqual(iso.weeksBetween(WeekRef(year: 2026, week: 52), WeekRef(year: 2027, week: 2)), 3)
        var berlin = makeCalendar(.iso, locale: locale, timeZone: TimeZone(identifier: "Europe/Berlin")!)
        berlin.locale = locale
        XCTAssertEqual(berlin.weeksBetween(WeekRef(year: 2026, week: 10), WeekRef(year: 2026, week: 20)), 10)
    }

    func testUSNumbering() {
        // US weeks start Sunday; Jan 1 is always in week 1.
        XCTAssertEqual(us.weekRef(for: date(2027, 1, 1)).week, 1)
        XCTAssertEqual(us.component(.weekday, from: us.startOfWeek(WeekRef(year: 2026, week: 41))!), 1)
    }
}

final class QueryTests: CalTestCase {
    func week(_ q: String, _ c: QueryContext) -> WeekRef? { QueryEngine.evaluate(q, c).first?.week }

    func testWeekTokens() {
        let c = ctx(2026, 10, 5)
        for q in ["46", "CW46", "cw 46", "KW46", "w46", "week 46", "46 2026", "2026-W46", "2026w46", "46/2026"] {
            XCTAssertEqual(week(q, c), WeekRef(year: 2026, week: 46), q)
        }
        XCTAssertEqual(week("3 2027", c), WeekRef(year: 2027, week: 3))
    }

    func testLateYearHeuristic() {
        XCTAssertEqual(week("5", ctx(2026, 10, 5)), WeekRef(year: 2027, week: 5))
        XCTAssertEqual(week("5", ctx(2026, 3, 5)), WeekRef(year: 2026, week: 5))
    }

    func testInvalidWeeks() {
        let c = ctx(2026, 10, 5)
        XCTAssertEqual(QueryEngine.evaluate("54", c).first?.kind, .error)
        XCTAssertEqual(QueryEngine.evaluate("2027-W53", c).first?.kind, .error)
        XCTAssertEqual(week("2026-W53", c), WeekRef(year: 2026, week: 53))
    }

    func testRelative() {
        let c = ctx(2026, 10, 5)
        XCTAssertEqual(week("+6w", c), WeekRef(year: 2026, week: 47))
        XCTAssertEqual(week("+6", c), WeekRef(year: 2026, week: 47))
        XCTAssertEqual(week("in 6 weeks", c), WeekRef(year: 2026, week: 47))
        XCTAssertEqual(week("-2w", c), WeekRef(year: 2026, week: 39))
        XCTAssertEqual(week("3 weeks ago", c), WeekRef(year: 2026, week: 38))
        XCTAssertEqual(week("+14d", c), WeekRef(year: 2026, week: 43))
        XCTAssertEqual(week("+13", c), WeekRef(year: 2027, week: 1))
        XCTAssertEqual(QueryEngine.evaluate("+6w", c).first?.kind, .relative)
    }

    func testRanges() {
        let c = ctx(2026, 10, 5)
        let r = QueryEngine.evaluate("46-50", c).first!
        XCTAssertEqual(r.kind, .range)
        XCTAssertEqual(r.weeks, 5)
        XCTAssertEqual(r.workdays, 25)
        let wrap = QueryEngine.evaluate("CW52 – CW2", c).first!
        XCTAssertEqual(wrap.kind, .range)
        XCTAssertEqual(wrap.weeks, 4) // 52, 53, 1, 2
        XCTAssertEqual(QueryEngine.evaluate("46 to 48", c).first?.weeks, 3)
    }

    func testRangeWithHolidays() {
        let c = ctx(2026, 10, 5, holidays: HolidayStore(regions: [.de]))
        let r = QueryEngine.evaluate("52-53", c).first!
        // 2026-W52: Dec 21–27 (25, 26 holidays; 26 is Saturday) → 4 workdays; W53: Dec 28–Jan 3 (Jan 1 holiday) → 4
        XCTAssertEqual(r.workdays, 8)
    }

    func testCountdown() {
        let c = ctx(2026, 10, 5)
        let r = QueryEngine.evaluate("until CW52", c).first!
        XCTAssertEqual(r.kind, .countdown)
        XCTAssertEqual(r.weeks, 11)
        XCTAssertEqual(QueryEngine.evaluate("to 46", c).first?.weeks, 5)
        XCTAssertEqual(QueryEngine.evaluate("today -> cw46", c).first?.kind, .countdown)
    }

    func testDates() {
        let c = ctx(2026, 10, 5)
        let r = QueryEngine.evaluate("December 12, 2026", c).first!
        XCTAssertEqual(r.kind, .date)
        XCTAssertEqual(r.week, WeekRef(year: 2026, week: 50))
    }

    func testKeywords() {
        let c = ctx(2026, 10, 5)
        XCTAssertEqual(week("next week", c), WeekRef(year: 2026, week: 42))
        XCTAssertEqual(week("today", c), WeekRef(year: 2026, week: 41))
    }

    func testISOvsUSWarning() {
        // 2026: Jan 1 is Thursday → ISO W1 starts Dec 29. US week of Wed Oct 7 is 41 too? Check a mismatch year.
        let c = ctx(2027, 3, 1) // 2027: Jan 1 Friday → ISO W1 starts Jan 4; US week numbers run one ahead.
        let r = QueryEngine.evaluate("10", c).first!
        XCTAssertNotNil(r.detail)
        XCTAssertTrue(r.detail!.contains("US numbering: CW11"), r.detail!)
    }

    func testMilestoneSearch() {
        let m = Milestone(name: "Release 5.0", week: WeekRef(year: 2026, week: 46))
        let c = ctx(2026, 10, 5, milestones: [m])
        let r = QueryEngine.evaluate("release", c).first!
        XCTAssertEqual(r.kind, .milestone)
        XCTAssertEqual(r.weeks, 5)
        XCTAssertEqual(r.workdays, 30) // Oct 5 (Mon) → Nov 15: 6 full work weeks
        XCTAssertTrue(QueryEngine.evaluate("", c).contains { $0.kind == .milestone })
    }

    func testCopyText() {
        let c = ctx(2026, 10, 5)
        XCTAssertEqual(QueryEngine.evaluate("46", c).first?.copyText, "CW46 (9–15 Nov)")
        XCTAssertEqual(QueryEngine.evaluate("3 2027", c).first?.copyText, "CW3 2027 (18–24 Jan)")
    }
}

final class HolidayTests: XCTestCase {
    func testEaster() {
        XCTAssertEqual(HolidayRules.easter(2026), Day(2026, 4, 5))
        XCTAssertEqual(HolidayRules.easter(2027), Day(2027, 3, 28))
        XCTAssertEqual(HolidayRules.easter(2025), Day(2025, 4, 20))
        XCTAssertEqual(HolidayRules.easter(2030), Day(2030, 4, 21))
    }

    func testUSRules() {
        let us = Set(HolidayRules.holidays(.us, year: 2026).map(\.day))
        XCTAssertTrue(us.contains(Day(2026, 11, 26)))  // Thanksgiving
        XCTAssertTrue(us.contains(Day(2026, 5, 25)))   // Memorial Day
        XCTAssertTrue(us.contains(Day(2026, 7, 3)))    // July 4 is Saturday → observed Friday
        XCTAssertTrue(us.contains(Day(2026, 1, 19)))   // MLK
    }

    func testUKSubstitutes() {
        // 2027: Dec 25 Saturday, Dec 26 Sunday → Mon 27, Tue 28
        let uk = Set(HolidayRules.holidays(.gb, year: 2027).map(\.day))
        XCTAssertTrue(uk.contains(Day(2027, 12, 27)))
        XCTAssertTrue(uk.contains(Day(2027, 12, 28)))
    }

    func testDenmarkAndSweden() {
        let dk = Set(HolidayRules.holidays(.dk, year: 2026).map(\.day))
        XCTAssertTrue(dk.contains(Day(2026, 4, 2)))   // Maundy Thursday
        XCTAssertTrue(dk.contains(Day(2026, 5, 14)))  // Ascension
        XCTAssertFalse(dk.contains(Day(2026, 5, 1)))
        let se = HolidayRules.holidays(.se, year: 2026)
        XCTAssertTrue(se.contains { $0.day == Day(2026, 6, 19) && $0.name == "Midsommarafton" })
    }

    func testWorkingDays() {
        var cal = makeCalendar(.iso, locale: Locale(identifier: "en_IN"), timeZone: TimeZone(identifier: "Asia/Kolkata")!)
        cal.locale = Locale(identifier: "en_IN") // India's locale weekend is Sunday-only; we still use Mon–Fri
        let store = HolidayStore(regions: [.in])
        // 2026-W41 (Oct 5–11): no holidays → 5
        XCTAssertEqual(store.workingDays(in: WeekRef(year: 2026, week: 41), cal: cal), 5)
        // 2026-W40 (Sep 28–Oct 4): Oct 2 Gandhi Jayanti (Fri) → 4
        XCTAssertEqual(store.workingDays(in: WeekRef(year: 2026, week: 40), cal: cal), 4)
    }

    func testICS() {
        let ics = """
        BEGIN:VCALENDAR
        BEGIN:VEVENT
        DTSTART;VALUE=DATE:20261109
        DTEND;VALUE=DATE:20261111
        SUMMARY:Diwali\\, office closed
        END:VEVENT
        BEGIN:VEVENT
        DTSTART:20261225T000000Z
        SUMMARY:Christmas
        END:VEVENT
        END:VCALENDAR
        """
        let hs = ICSParser.parse(ics, source: "Company")
        XCTAssertEqual(hs.map(\.day), [Day(2026, 11, 9), Day(2026, 11, 10), Day(2026, 12, 25)])
        XCTAssertEqual(hs.first?.name, "Diwali, office closed")
    }
}

final class PlanningTests: XCTestCase {
    let cal = makeCalendar(.iso, locale: Locale(identifier: "en_GB"), timeZone: TimeZone(identifier: "Asia/Kolkata")!)
    func date(_ y: Int, _ m: Int, _ d: Int) -> Date { cal.date(from: DateComponents(year: y, month: m, day: d, hour: 10))! }

    func testSprints() {
        var cfg = SprintConfig()
        cfg.enabled = true; cfg.lengthWeeks = 2; cfg.anchor = Day(2026, 1, 7) /* Wed of W2 */; cfg.firstNumber = 1
        let info = Sprints.info(for: date(2026, 10, 5), config: cfg, cal: cal, holidays: .empty)!
        // Sprint 1 starts Mon Jan 5 (W2). Sprint 20 covers W40–W41; Oct 5 is its 6th working day.
        XCTAssertEqual(info.number, 20)
        XCTAssertEqual(info.firstWeek, WeekRef(year: 2026, week: 40))
        XCTAssertEqual(info.lastWeek, WeekRef(year: 2026, week: 41))
        XCTAssertEqual(info.workday, 6)
        XCTAssertEqual(info.workdaysTotal, 10)
        XCTAssertTrue(Sprints.startsSprint(WeekRef(year: 2026, week: 40), config: cfg, cal: cal))
        XCTAssertFalse(Sprints.startsSprint(WeekRef(year: 2026, week: 41), config: cfg, cal: cal))
    }

    func testProgramIncrements() {
        var cfg = SprintConfig()
        cfg.enabled = true; cfg.lengthWeeks = 2; cfg.anchor = Day(2026, 1, 5); cfg.sprintsPerPI = 5; cfg.firstPI = 1
        let info = Sprints.info(for: date(2026, 10, 5), config: cfg, cal: cal, holidays: .empty)!
        // index 19 → PI 4 (index 3), sprint 5 of 5
        XCTAssertEqual(info.pi, 4)
        XCTAssertEqual(info.sprintInPI, 5)
    }

    func testQuarters() {
        var q = QuarterConfig()
        XCTAssertEqual(Quarters.info(for: date(2026, 10, 5), config: q, cal: cal)?.label, "Q4 2026")
        q.fiscalStartMonth = 4 // India FY: Apr–Mar, named by end year
        XCTAssertEqual(Quarters.info(for: date(2026, 10, 5), config: q, cal: cal)?.label, "Q3 FY27")
        XCTAssertEqual(Quarters.info(for: date(2027, 2, 5), config: q, cal: cal)?.label, "Q4 FY27")
        q.mode = .weeks
        XCTAssertEqual(Quarters.info(for: date(2026, 10, 5), config: q, cal: cal)?.label, "Q4 2026") // W41 → Q4
    }

    func testMilestoneStatus() {
        let m = Milestone(name: "Code freeze", week: WeekRef(year: 2026, week: 42))
        let st = Milestones.status(m, now: date(2026, 10, 7), cal: cal, holidays: .empty)!
        XCTAssertEqual(st.weeksAway, 1)
        XCTAssertEqual(st.workdaysLeft, 8) // Wed–Fri (3) + next week (5)
    }
}
