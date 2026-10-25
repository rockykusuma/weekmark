import SwiftUI

struct YearMonth: Hashable {
    let year: Int
    let month: Int
}

/// The whole calendar year as mini months with a week-number column. Tap a month to open it full size.
struct YearTab: View {
    @EnvironmentObject private var store: PlanStore
    @State private var year: Int?

    var body: some View {
        NavigationStack {
            TimelineView(.periodic(from: .now, by: 60)) { ctx in
                let snap = WeekSnapshot(now: ctx.date, numbering: store.numbering)
                let plan = store.snapshot
                let thisYear = snap.cal.component(.year, from: snap.now)
                let shown = year ?? thisYear
                ScrollView {
                    VStack(alignment: .leading, spacing: 14) {
                        Text(subtitle(snap, shown))
                            .font(.subheadline.weight(.medium))
                            .foregroundStyle(.secondary)
                        LazyVGrid(columns: [GridItem(.adaptive(minimum: 108), spacing: 10)], spacing: 10) {
                            ForEach(1...12, id: \.self) { m in
                                NavigationLink(value: YearMonth(year: shown, month: m)) {
                                    MiniMonthView(snap: snap, plan: plan, year: shown, month: m)
                                }
                                .buttonStyle(.plain)
                            }
                        }
                    }
                    .padding(.horizontal, 16)
                    .padding(.bottom, 24)
                }
                .background(Color(.systemGroupedBackground))
                .simultaneousGesture(DragGesture(minimumDistance: 40).onEnded { v in
                    guard abs(v.translation.width) > abs(v.translation.height) * 2 else { return }
                    withAnimation { year = shown + (v.translation.width < 0 ? 1 : -1) }
                })
                .navigationTitle(String(shown))
                .toolbar {
                    ToolbarItemGroup(placement: .topBarTrailing) {
                        if shown != thisYear {
                            Button("Today") { withAnimation { year = nil } }
                        }
                        Button { withAnimation { year = shown - 1 } } label: { Image(systemName: "chevron.left") }
                            .accessibilityLabel("Previous year")
                        Button { withAnimation { year = shown + 1 } } label: { Image(systemName: "chevron.right") }
                            .accessibilityLabel("Next year")
                    }
                }
            }
            .navigationDestination(for: YearMonth.self) { MonthDetailView(ym: $0) }
        }
    }

    private func subtitle(_ snap: WeekSnapshot, _ year: Int) -> String {
        var s = "\(snap.cal.weeksIn(weekYear: year)) calendar weeks"
        if year == snap.weekYear { s += " · week \(snap.weekNumber) now, \(snap.weeksLeft) to go" }
        return s
    }
}

/// One month of the year grid: same marks as MonthGridView, sized to fit three across on an iPhone.
struct MiniMonthView: View {
    let snap: WeekSnapshot
    let plan: PlanSnapshot
    let year: Int
    let month: Int
    private let cwWidth: CGFloat = 14

    var body: some View {
        let first = snap.cal.date(from: DateComponents(year: year, month: month, day: 1))!
        let msByWeek = Dictionary(grouping: plan.milestones, by: \.week)
        VStack(alignment: .leading, spacing: 2) {
            Text(snap.fmt.format(first, "MMM"))
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(snap.isIn(snap.now, month: first) ? Color.accentColor : Color.primary)
                .padding(.leading, 2)
            HStack(spacing: 0) {
                Color.clear.frame(width: cwWidth, height: 1)
                ForEach(0..<7, id: \.self) { i in
                    Text(snap.weekdaySymbol(snap.days[i], veryShort: true)).frame(maxWidth: .infinity)
                }
            }
            .font(.system(size: 7.5, weight: .semibold))
            .foregroundStyle(.tertiary)
            // monthRows is always 6 rows, so every month has the same height.
            ForEach(snap.monthRows(month: first)) { row in
                let inMonth = row.days.contains { snap.isIn($0, month: first) }
                let ms = msByWeek[row.ref]?.first
                HStack(spacing: 0) {
                    Text(inMonth ? "\(row.ref.week)" : "")
                        .font(.system(size: 8.5, weight: .bold, design: .rounded))
                        .foregroundStyle(row.isCurrent ? Color.accentColor : ms?.color.color ?? Color.secondary)
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                        .frame(width: cwWidth)
                    ForEach(0..<7, id: \.self) { i in
                        let d = row.days[i]
                        if snap.isIn(d, month: first) {
                            let today = snap.isToday(d)
                            let hol = plan.holidays.isHoliday(Day(d, calendar: snap.cal))
                            Text(snap.dayNumber(d))
                                .font(.system(size: 9, weight: today ? .bold : .regular, design: .rounded))
                                .foregroundStyle(today ? AnyShapeStyle(Color.white)
                                                 : hol ? AnyShapeStyle(Color.red)
                                                 : snap.isPast(d) ? AnyShapeStyle(.secondary) : AnyShapeStyle(.primary))
                                .lineLimit(1)
                                .fixedSize()
                                .frame(width: 13, height: 13)
                                .background { if today { Circle().fill(Color.accentColor) } }
                                .frame(maxWidth: .infinity)
                        } else {
                            Color.clear.frame(maxWidth: .infinity, maxHeight: 13)
                        }
                    }
                }
                .monospacedDigit()
                .padding(.vertical, 0.5)
                .background {
                    if row.isCurrent { RoundedRectangle(cornerRadius: 4, style: .continuous).fill(Color.accentColor.opacity(0.14)) }
                }
            }
        }
        .padding(.horizontal, 5)
        .padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(Color(.secondarySystemGroupedBackground)))
        .contentShape(Rectangle())
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(snap.fmt.format(first, "MMMMyyyy"))
    }
}

/// A month full size: tap a week for its details; lists the month's holidays and milestones.
struct MonthDetailView: View {
    @EnvironmentObject private var store: PlanStore
    let ym: YearMonth
    @State private var detail: WeekRef?

    var body: some View {
        let snap = WeekSnapshot(now: Date(), numbering: store.numbering)
        let plan = store.snapshot
        let month = snap.cal.date(from: DateComponents(year: ym.year, month: ym.month, day: 1))!
        let days = snap.cal.range(of: .day, in: .month, for: month)!.map { snap.cal.date(byAdding: .day, value: $0 - 1, to: month)! }
        let holidays = days.flatMap { d in plan.holidays.holidays(on: d, cal: snap.cal).map { (d, $0) } }
        let weeks = Set(snap.monthRows(month: month).filter { r in r.days.contains { snap.isIn($0, month: month) } }.map(\.ref))
        let milestones = plan.milestones.filter { weeks.contains($0.week) }.sorted { $0.week < $1.week }
        List {
            Section {
                MonthGridView(snap: snap, month: month, plan: plan, target: detail) { detail = $0 }
                    .padding(.vertical, 6)
            } footer: {
                Text("Tap a week for its dates, working days and copy text.")
            }
            if !holidays.isEmpty {
                Section("Holidays") {
                    ForEach(holidays, id: \.1) { d, h in
                        HStack {
                            Text(h.name)
                            Spacer()
                            Text("\(snap.fmt.format(d, "EEEdMMM")) · CW\(snap.cal.weekRef(for: d).week)")
                                .foregroundStyle(.secondary)
                        }
                    }
                }
            }
            if !milestones.isEmpty {
                Section("Milestones") {
                    ForEach(milestones) { m in
                        MilestoneRow(milestone: m, snap: snap, holidays: plan.holidays)
                            .contentShape(Rectangle())
                            .onTapGesture { detail = m.week }
                    }
                }
            }
        }
        .navigationTitle(snap.fmt.format(month, "MMMMyyyy"))
        .navigationBarTitleDisplayMode(.inline)
        .sheet(item: $detail) { ref in
            WeekDetailSheet(ref: ref).presentationDetents([.medium, .large])
        }
    }
}
