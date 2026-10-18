import SwiftUI

// Reusable pieces shared by the iPhone app, its widgets, and the Watch.

struct PlanningChips: View {
    let snap: WeekSnapshot
    let plan: PlanSnapshot
    var scale: CGFloat = 1

    var body: some View {
        let q = Quarters.info(for: snap.now, config: plan.quarter, cal: snap.cal)
        let sp = Sprints.info(for: snap.now, config: plan.sprint, cal: snap.cal, holidays: plan.holidays)
        if q != nil || sp != nil {
            HStack(spacing: 6 * scale) {
                if let q { Chip(text: q.label, icon: "chart.pie", scale: scale) }
                if let sp {
                    Chip(text: "\(plan.sprint.name) \(sp.number) · day \(max(sp.workday, 1))/\(sp.workdaysTotal)",
                         icon: "arrow.triangle.2.circlepath", scale: scale)
                    if let pi = sp.pi, let n = sp.sprintInPI {
                        Chip(text: "PI \(pi) · \(n)/\(sp.sprintsPerPI)", icon: "square.stack.3d.up", scale: scale)
                    }
                }
            }
            .lineLimit(1)
            .minimumScaleFactor(0.7)
        }
    }
}

struct Chip: View {
    let text: String
    let icon: String
    var scale: CGFloat = 1
    var body: some View {
        HStack(spacing: 4 * scale) {
            Image(systemName: icon).font(.system(size: 10 * scale, weight: .semibold))
            Text(text).font(.system(size: 12 * scale, weight: .semibold, design: .rounded))
        }
        .padding(.horizontal, 8 * scale).padding(.vertical, 4 * scale)
        .background(Capsule().fill(.primary.opacity(0.08)))
        .foregroundStyle(.secondary)
    }
}

struct WeekStripView: View {
    let snap: WeekSnapshot
    let holidays: HolidayStore
    var scale: CGFloat = 1
    var showSymbols = true

    var body: some View {
        HStack(spacing: 0) {
            ForEach(0..<7, id: \.self) { i in
                let d = snap.days[i]
                let today = snap.isToday(d)
                let hol = holidays.isHoliday(Day(d, calendar: snap.cal))
                VStack(spacing: 4 * scale) {
                    if showSymbols {
                        Text(snap.weekdaySymbol(d, veryShort: scale < 0.9).uppercased())
                            .font(.system(size: 10 * scale, weight: .semibold))
                            .foregroundStyle(today ? Color.accentColor : .secondary)
                    }
                    ZStack {
                        if today { Circle().fill(Color.accentColor) }
                        Text(snap.dayNumber(d))
                            .font(.system(size: 15 * scale, weight: today ? .bold : .medium, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(today ? Color.white : hol ? Color.red : snap.isPast(d) ? Color.secondary : Color.primary)
                    }
                    .frame(width: 30 * scale, height: 30 * scale)
                }
                .frame(maxWidth: .infinity)
            }
        }
    }
}

struct WeekProgressBar: View {
    let progress: Double
    var height: CGFloat = 5
    var body: some View {
        GeometryReader { g in
            ZStack(alignment: .leading) {
                Capsule().fill(.primary.opacity(0.12))
                Capsule().fill(Color.accentColor).frame(width: max(height, g.size.width * progress))
            }
        }
        .frame(height: height)
    }
}

struct YearRing: View {
    let snap: WeekSnapshot
    var size: CGFloat = 52
    var body: some View {
        ZStack {
            Circle().stroke(.primary.opacity(0.12), lineWidth: size * 0.1)
            Circle().trim(from: 0, to: snap.yearProgress)
                .stroke(Color.accentColor, style: StrokeStyle(lineWidth: size * 0.1, lineCap: .round))
                .rotationEffect(.degrees(-90))
            VStack(spacing: 0) {
                Text("\(snap.weekNumber)").font(.system(size: size * 0.27, weight: .bold, design: .rounded))
                Text("of \(snap.weeksInYear)").font(.system(size: size * 0.16, weight: .medium)).foregroundStyle(.secondary)
            }
        }
        .frame(width: size, height: size)
    }
}

struct MilestoneRow: View {
    let milestone: Milestone
    let snap: WeekSnapshot
    let holidays: HolidayStore
    var scale: CGFloat = 1

    var body: some View {
        let st = Milestones.status(milestone, now: snap.now, cal: snap.cal, holidays: holidays)
        HStack(spacing: 7 * scale) {
            Image(systemName: "flag.fill").font(.system(size: 10 * scale)).foregroundStyle(milestone.color.color)
            Text(milestone.name).font(.system(size: 14 * scale, weight: .semibold)).lineLimit(1)
            Spacer(minLength: 4)
            Text("CW\(milestone.week.week)").font(.system(size: 13 * scale, weight: .bold, design: .rounded))
                .foregroundStyle(milestone.color.color)
            if let st {
                Text(st.weeksAway == 0 ? "\(st.workdaysLeft)d left" : "\(st.weeksAway)w · \(st.workdaysLeft)d")
                    .font(.system(size: 12 * scale, weight: .medium, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(.secondary)
            }
        }
    }
}

/// Month grid with a CW column. `onTap` makes rows tappable (app); nil for widgets.
struct MonthGridView: View {
    let snap: WeekSnapshot
    let month: Date
    let plan: PlanSnapshot
    var target: WeekRef? = nil
    var scale: CGFloat = 1
    var onTap: ((WeekRef) -> Void)? = nil

    var body: some View {
        let rows = snap.monthRows(month: month)
        let holidays = plan.holidays
        let msByWeek = Dictionary(grouping: plan.milestones, by: \.week)
        let cw = 30 * scale
        VStack(spacing: 3 * scale) {
            HStack(spacing: 0) {
                Text("CW").frame(width: cw)
                ForEach(0..<7, id: \.self) { i in
                    Text(snap.weekdaySymbol(snap.days[i], veryShort: true)).frame(maxWidth: .infinity)
                }
            }
            .font(.system(size: 10 * scale, weight: .semibold))
            .foregroundStyle(.tertiary)
            ForEach(rows) { row in
                let isTarget = target == row.ref
                let ms = msByWeek[row.ref]?.first
                let line = HStack(spacing: 0) {
                    HStack(spacing: 1.5 * scale) {
                        if let ms { Image(systemName: "flag.fill").font(.system(size: 7 * scale)).foregroundStyle(ms.color.color) }
                        Text("\(row.ref.week)")
                            .font(.system(size: 11 * scale, weight: .bold, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(isTarget ? Color.orange : row.isCurrent ? Color.accentColor : ms?.color.color ?? Color.secondary)
                    }
                    .frame(width: cw)
                    ForEach(0..<7, id: \.self) { i in
                        let d = row.days[i]
                        let today = snap.isToday(d)
                        let inMonth = snap.isIn(d, month: month)
                        let hol = holidays.isHoliday(Day(d, calendar: snap.cal))
                        Text(snap.dayNumber(d))
                            .font(.system(size: 13 * scale, weight: today ? .bold : .regular, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(today ? AnyShapeStyle(Color.white)
                                             : !inMonth ? AnyShapeStyle(.quaternary)
                                             : hol ? AnyShapeStyle(Color.red) : AnyShapeStyle(.primary))
                            .frame(width: 24 * scale, height: 24 * scale)
                            .background { if today { Circle().fill(Color.accentColor) } }
                            .frame(maxWidth: .infinity)
                    }
                }
                .padding(.vertical, 1 * scale)
                .background {
                    let shape = RoundedRectangle(cornerRadius: 7 * scale, style: .continuous)
                    if isTarget { shape.fill(Color.orange.opacity(0.2)).overlay(shape.strokeBorder(Color.orange.opacity(0.85), lineWidth: 1.2)) }
                    else if row.isCurrent { shape.fill(Color.accentColor.opacity(0.14)) }
                }
                if let onTap {
                    line.contentShape(Rectangle()).onTapGesture { onTap(row.ref) }
                } else {
                    line
                }
            }
        }
    }
}
