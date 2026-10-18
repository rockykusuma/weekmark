import SwiftUI
import WidgetKit

struct WeekEntry: TimelineEntry {
    let date: Date
    let plan: PlanSnapshot
    var snap: WeekSnapshot { WeekSnapshot(now: date, numbering: plan.numbering) }
}

/// Hourly entries for two days (keeps the week-progress ring fresh), then reload.
struct WeekProvider: TimelineProvider {
    func placeholder(in context: Context) -> WeekEntry { WeekEntry(date: Date(), plan: PlanSnapshot.load()) }

    func getSnapshot(in context: Context, completion: @escaping (WeekEntry) -> Void) {
        completion(WeekEntry(date: Date(), plan: PlanSnapshot.load()))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<WeekEntry>) -> Void) {
        WidgetSync.pull()
        let plan = PlanSnapshot.load()
        let cal = plan.calendar
        let now = Date()
        let hour = cal.dateInterval(of: .hour, for: now)!.start
        var dates = [now]
        for h in 1...48 { dates.append(cal.date(byAdding: .hour, value: h, to: hour)!) }
        completion(Timeline(entries: dates.map { WeekEntry(date: $0, plan: plan) }, policy: .atEnd))
    }
}

/// The milestone or sprint line shown under the week on small surfaces.
func nextUpLine(_ entry: WeekEntry) -> String? {
    let snap = entry.snap
    if let m = Milestones.upcoming(entry.plan.milestones, now: snap.now, cal: snap.cal).first,
       let st = Milestones.status(m, now: snap.now, cal: snap.cal, holidays: entry.plan.holidays) {
        return st.weeksAway == 0 ? "\(m.name) · \(st.workdaysLeft)d left" : "\(m.name) · \(st.weeksAway)w"
    }
    if let sp = Sprints.info(for: snap.now, config: entry.plan.sprint, cal: snap.cal, holidays: entry.plan.holidays) {
        return "\(entry.plan.sprint.name) \(sp.number) · day \(max(sp.workday, 1))/\(sp.workdaysTotal)"
    }
    if let q = Quarters.info(for: snap.now, config: entry.plan.quarter, cal: snap.cal) {
        return "\(q.label) · \(snap.weeksLeft) wks left"
    }
    return nil
}

// MARK: Accessory views (Lock Screen, StandBy accessories, Watch complications)

struct CircularWeekView: View {
    let entry: WeekEntry
    var body: some View {
        let snap = entry.snap
        Gauge(value: snap.weekProgress) {
            Text("CW")
        } currentValueLabel: {
            Text("\(snap.weekNumber)").font(.system(.title2, design: .rounded).weight(.bold))
        }
        .gaugeStyle(.accessoryCircular)
        .widgetAccentable()
        .accessibilityLabel("Calendar week \(snap.weekNumber)")
    }
}

struct RectangularWeekView: View {
    let entry: WeekEntry
    var body: some View {
        let snap = entry.snap
        VStack(alignment: .leading, spacing: 1) {
            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text("CW\(snap.weekNumber)").font(.system(.headline, design: .rounded).weight(.bold)).widgetAccentable()
                Text(snap.rangeText).font(.caption).foregroundStyle(.secondary)
            }
            if let line = nextUpLine(entry) {
                Text(line).font(.caption).lineLimit(1)
            }
            ProgressView(value: snap.weekProgress).tint(.accentColor).widgetAccentable()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct InlineWeekView: View {
    let entry: WeekEntry
    var body: some View {
        Text("CW\(entry.snap.weekNumber) · \(entry.snap.rangeText)")
    }
}

// MARK: Home Screen / StandBy views (iPhone)

struct SmallWeekView: View {
    let entry: WeekEntry
    var body: some View {
        let snap = entry.snap
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("CALENDAR WEEK").font(.system(size: 9, weight: .semibold)).tracking(1.1).foregroundStyle(.secondary)
                Spacer()
                Text(String(snap.weekYear)).font(.system(size: 10, weight: .semibold, design: .rounded)).foregroundStyle(.secondary)
            }
            Text("\(snap.weekNumber)")
                .font(.system(size: 64, weight: .bold, design: .rounded))
                .foregroundStyle(Color.accentColor)
                .minimumScaleFactor(0.6)
                .padding(.vertical, -6)
            Text(snap.rangeText).font(.system(size: 13, weight: .semibold)).foregroundStyle(.secondary)
            Spacer(minLength: 4)
            if let line = nextUpLine(entry) {
                Text(line).font(.system(size: 11, weight: .medium)).lineLimit(1).minimumScaleFactor(0.8)
                    .padding(.bottom, 4)
            }
            WeekProgressBar(progress: snap.weekProgress, height: 4)
        }
        .accessibilityElement(children: .combine)
    }
}

struct MediumWeekView: View {
    let entry: WeekEntry
    var body: some View {
        let snap = entry.snap
        HStack(alignment: .top, spacing: 14) {
            VStack(alignment: .leading, spacing: 0) {
                Text("CALENDAR WEEK").font(.system(size: 9, weight: .semibold)).tracking(1.1).foregroundStyle(.secondary)
                Text("\(snap.weekNumber)")
                    .font(.system(size: 60, weight: .bold, design: .rounded))
                    .foregroundStyle(Color.accentColor)
                    .padding(.vertical, -6)
                Text(snap.rangeText).font(.system(size: 13, weight: .semibold)).foregroundStyle(.secondary)
                Spacer(minLength: 0)
                Text("\(snap.weeksLeft) wks left in \(String(snap.weekYear))").font(.system(size: 10, weight: .medium)).foregroundStyle(.secondary)
            }
            VStack(alignment: .leading, spacing: 8) {
                WeekStripView(snap: snap, holidays: entry.plan.holidays, scale: 0.72)
                WeekProgressBar(progress: snap.weekProgress, height: 4)
                if let m = Milestones.upcoming(entry.plan.milestones, now: snap.now, cal: snap.cal).first {
                    MilestoneRow(milestone: m, snap: snap, holidays: entry.plan.holidays, scale: 0.8)
                } else if let line = nextUpLine(entry) {
                    Text(line).font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary).lineLimit(1)
                }
                Spacer(minLength: 0)
            }
        }
    }
}

struct LargeWeekView: View {
    let entry: WeekEntry
    var body: some View {
        let snap = entry.snap
        let next = Milestones.upcoming(entry.plan.milestones, now: snap.now, cal: snap.cal).first
        VStack(alignment: .leading, spacing: 7) {
            HStack(alignment: .center) {
                Text("\(snap.weekNumber)").font(.system(size: 44, weight: .bold, design: .rounded))
                    .foregroundStyle(Color.accentColor)
                VStack(alignment: .leading, spacing: 1) {
                    Text("CALENDAR WEEK").font(.system(size: 9, weight: .semibold)).tracking(1.1).foregroundStyle(.secondary)
                    Text(snap.rangeText).font(.system(size: 14, weight: .semibold))
                    Text("\(snap.weeksLeft) wks left in \(String(snap.weekYear))").font(.system(size: 10, weight: .medium)).foregroundStyle(.secondary)
                }
                Spacer()
                YearRing(snap: snap, size: 40)
            }
            PlanningChips(snap: snap, plan: entry.plan, scale: 0.72)
            if let next { MilestoneRow(milestone: next, snap: snap, holidays: entry.plan.holidays, scale: 0.78) }
            Spacer(minLength: 0)
            MonthGridView(snap: snap, month: snap.displayedMonth(offset: 0), plan: entry.plan, scale: 0.7)
        }
    }
}
