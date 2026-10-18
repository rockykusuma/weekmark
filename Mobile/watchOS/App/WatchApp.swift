import SwiftUI

@main
struct WeekmarkWatchApp: App {
    @StateObject private var store = PlanStore.shared
    @Environment(\.scenePhase) private var phase
    var body: some Scene {
        WindowGroup {
            WatchRoot()
                .environmentObject(store)
                .onChange(of: phase) { _, p in if p == .active { store.syncNow() } }
        }
    }
}

struct WatchRoot: View {
    @EnvironmentObject private var store: PlanStore

    var body: some View {
        NavigationStack {
            TimelineView(.periodic(from: .now, by: 60)) { ctx in
                let snap = WeekSnapshot(now: ctx.date, numbering: store.numbering)
                let plan = store.snapshot
                List {
                    Section {
                        VStack(alignment: .leading, spacing: 0) {
                            Text("CALENDAR WEEK").font(.system(size: 10, weight: .semibold)).foregroundStyle(.secondary)
                            Text("\(snap.weekNumber)")
                                .font(.system(size: 60, weight: .bold, design: .rounded))
                                .foregroundStyle(Color.accentColor)
                                .padding(.vertical, -6)
                            Text(snap.rangeText).font(.headline)
                            if let line = nextUpLine(WeekEntry(date: ctx.date, plan: plan)) {
                                Text(line).font(.caption2).foregroundStyle(.secondary).lineLimit(2)
                            }
                        }
                        .listRowBackground(Color.clear)
                        WeekStripView(snap: snap, holidays: plan.holidays, scale: 0.62)
                        WeekProgressBar(progress: snap.weekProgress, height: 4)
                            .listRowBackground(Color.clear)
                    }
                    let upcoming = Array(Milestones.upcoming(plan.milestones, now: snap.now, cal: snap.cal).prefix(3))
                    if !upcoming.isEmpty {
                        Section("Milestones") {
                            ForEach(upcoming) { m in
                                NavigationLink(value: m.week) {
                                    MilestoneRow(milestone: m, snap: snap, holidays: plan.holidays, scale: 0.78)
                                }
                            }
                        }
                    }
                    Section("Weeks ahead") {
                        ForEach(1..<53, id: \.self) { off in
                            if let ref = snap.cal.weekRef(snap.current, adding: off) {
                                NavigationLink(value: ref) { WeekRow(ref: ref, plan: plan, cal: snap.cal) }
                            }
                        }
                    }
                }
                .navigationDestination(for: WeekRef.self) { WatchWeekDetail(ref: $0) }
            }
            .toolbar(.hidden, for: .navigationBar)
        }
    }
}

struct WeekRow: View {
    let ref: WeekRef
    let plan: PlanSnapshot
    let cal: Calendar
    var body: some View {
        let fmt = WeekFormatter(cal: cal)
        let ms = plan.milestones.first { $0.week == ref }
        let wd = plan.holidays.workingDays(in: ref, cal: cal)
        HStack {
            Text("\(ref.week)").font(.system(.body, design: .rounded).weight(.bold)).monospacedDigit()
                .foregroundStyle(ms?.color.color ?? Color.accentColor)
                .frame(width: 30, alignment: .leading)
            VStack(alignment: .leading, spacing: 0) {
                Text(fmt.shortRange(ref)).font(.footnote)
                if let ms { Text(ms.name).font(.caption2).foregroundStyle(ms.color.color).lineLimit(1) }
                else if wd < 5 { Text("\(wd) working days").font(.caption2).foregroundStyle(.red) }
            }
        }
    }
}

struct WatchWeekDetail: View {
    @EnvironmentObject private var store: PlanStore
    let ref: WeekRef
    var body: some View {
        let ctx = store.context()
        let r = QueryEngine.weekResult(ref, ctx)
        let fmt = WeekFormatter(cal: ctx.cal)
        List {
            VStack(alignment: .leading) {
                Text("CW\(ref.week)").font(.system(size: 36, weight: .bold, design: .rounded)).foregroundStyle(Color.accentColor)
                Text(fmt.longRange(ref)).font(.footnote)
                Text(r.subtitle).font(.caption2).foregroundStyle(.secondary)
            }
            .listRowBackground(Color.clear)
            ForEach(ctx.cal.days(of: ref), id: \.self) { d in
                let hol = ctx.holidays.holidays(on: d, cal: ctx.cal).first
                VStack(alignment: .leading) {
                    Text(fmt.format(d, "EEEdMMM")).font(.footnote)
                    if let hol { Text(hol.name).font(.caption2).foregroundStyle(.red) }
                }
            }
        }
        .navigationTitle("Week \(ref.week)")
    }
}
