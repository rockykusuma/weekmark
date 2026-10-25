import SwiftUI

struct WeekTab: View {
    @EnvironmentObject private var store: PlanStore
    @State private var monthOffset = 0
    @State private var detail: WeekRef?

    var body: some View {
        NavigationStack {
            TimelineView(.periodic(from: .now, by: 60)) { ctx in
                let snap = WeekSnapshot(now: ctx.date, numbering: store.numbering)
                let plan = store.snapshot
                ScrollView {
                    VStack(alignment: .leading, spacing: 16) {
                        header(snap)
                        PlanningChips(snap: snap, plan: plan)
                        card {
                            VStack(alignment: .leading, spacing: 12) {
                                WeekStripView(snap: snap, holidays: plan.holidays)
                                HStack {
                                    Text(snap.todayText)
                                    Spacer()
                                    Text("\(Int((snap.weekProgress * 100).rounded()))% of week").monospacedDigit()
                                }
                                .font(.footnote.weight(.medium)).foregroundStyle(.secondary)
                                WeekProgressBar(progress: snap.weekProgress)
                            }
                        }
                        let upcoming = Array(Milestones.upcoming(plan.milestones, now: snap.now, cal: snap.cal).prefix(4))
                        if !upcoming.isEmpty {
                            card {
                                VStack(alignment: .leading, spacing: 10) {
                                    Text("Milestones").font(.headline)
                                    ForEach(upcoming) { m in
                                        MilestoneRow(milestone: m, snap: snap, holidays: plan.holidays)
                                            .contentShape(Rectangle())
                                            .onTapGesture { detail = m.week }
                                    }
                                }
                            }
                        }
                        card { month(snap, plan) }
                    }
                    .padding(.horizontal, 16)
                    .padding(.bottom, 24)
                }
                .background(Color(.systemGroupedBackground))
                .refreshable { store.syncNow() }
            }
            .toolbar(.hidden, for: .navigationBar)
            .sheet(item: $detail) { ref in
                WeekDetailSheet(ref: ref).presentationDetents([.medium, .large])
            }
            #if DEBUG
            .task { if DebugLaunch.weekDetail { detail = store.context().current } }
            #endif
        }
    }

    private func header(_ snap: WeekSnapshot) -> some View {
        HStack(alignment: .top) {
            VStack(alignment: .leading, spacing: 0) {
                Text("CALENDAR WEEK")
                    .font(.system(size: 12, weight: .semibold)).tracking(1.6).foregroundStyle(.secondary)
                Text("\(snap.weekNumber)")
                    .font(.system(size: 96, weight: .bold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(Color.accentColor)
                    .contentTransition(.numericText())
                    .padding(.vertical, -8)
                Text(snap.rangeText).font(.title3.weight(.medium)).foregroundStyle(.secondary)
            }
            .accessibilityElement(children: .ignore)
            .accessibilityLabel("Calendar week \(snap.weekNumber), \(snap.rangeText)")
            Spacer()
            VStack(alignment: .trailing, spacing: 6) {
                Text(String(snap.weekYear)).font(.subheadline.weight(.semibold)).foregroundStyle(.secondary)
                YearRing(snap: snap, size: 58)
                Text("\(snap.weeksLeft) wks left").font(.caption.weight(.medium)).foregroundStyle(.secondary)
            }
        }
        .padding(.top, 12)
    }

    private func month(_ snap: WeekSnapshot, _ plan: PlanSnapshot) -> some View {
        let month = snap.displayedMonth(offset: monthOffset)
        return VStack(spacing: 10) {
            HStack {
                Text(snap.monthTitle(month)).font(.headline)
                Spacer()
                if monthOffset != 0 {
                    Button("Today") { withAnimation { monthOffset = 0 } }.font(.subheadline.weight(.semibold))
                }
                Button { withAnimation { monthOffset -= 1 } } label: { Image(systemName: "chevron.left") }
                    .accessibilityLabel("Previous month")
                Button { withAnimation { monthOffset += 1 } } label: { Image(systemName: "chevron.right") }
                    .accessibilityLabel("Next month")
            }
            .buttonStyle(.bordered).buttonBorderShape(.capsule).controlSize(.small)
            MonthGridView(snap: snap, month: month, plan: plan, target: detail) { detail = $0 }
                .gesture(DragGesture(minimumDistance: 30).onEnded { v in
                    withAnimation { monthOffset += v.translation.width < 0 ? 1 : -1 }
                })
        }
    }

    private func card<C: View>(@ViewBuilder _ content: () -> C) -> some View {
        content()
            .padding(16)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 20, style: .continuous).fill(Color(.secondarySystemGroupedBackground)))
    }
}

/// A week's details. ‹ › or a sideways swipe step to the previous/next week without closing the sheet.
struct WeekDetailSheet: View {
    @EnvironmentObject private var store: PlanStore
    @State private var ref: WeekRef
    @State private var addingMilestone = false
    @State private var copied = false

    init(ref: WeekRef) { _ref = State(initialValue: ref) }

    var body: some View {
        let ctx = store.context()
        let r = QueryEngine.weekResult(ref, ctx)
        let fmt = WeekFormatter(cal: ctx.cal)
        NavigationStack {
            List {
                Section {
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("CW\(ref.week)").font(.system(size: 44, weight: .bold, design: .rounded))
                                .foregroundStyle(ref == ctx.current ? Color.accentColor : Color.primary)
                                .contentTransition(.numericText())
                            Text(fmt.longRange(ref)).font(.headline)
                            Text(r.subtitle).font(.subheadline).foregroundStyle(.secondary)
                            if let d = r.detail { Text(d).font(.footnote).foregroundStyle(.secondary) }
                        }
                        Spacer()
                        if ref != ctx.current {
                            Button("This Week") { step(to: ctx.current) }
                                .buttonStyle(.bordered).buttonBorderShape(.capsule).controlSize(.small)
                        }
                    }
                    .padding(.vertical, 4)
                }
                Section("Days") {
                    ForEach(ctx.cal.days(of: ref), id: \.self) { d in
                        let hol = ctx.holidays.holidays(on: d, cal: ctx.cal)
                        HStack {
                            Text(fmt.format(d, "EEEEdMMM"))
                                .foregroundStyle(ctx.cal.isWorkday(d) && hol.isEmpty ? Color.primary : Color.secondary)
                            Spacer()
                            if let h = hol.first { Text(h.name).font(.footnote).foregroundStyle(.red) }
                        }
                    }
                }
                Section {
                    Button { copy(r.copyText ?? "") } label: {
                        Label(copied ? "Copied" : "Copy “\(r.copyText ?? "")”", systemImage: copied ? "checkmark" : "doc.on.doc")
                    }
                    ShareLink(item: r.copyText ?? "") { Label("Share", systemImage: "square.and.arrow.up") }
                    Button { addingMilestone = true } label: { Label("Add Milestone in CW\(ref.week)", systemImage: "flag") }
                }
            }
            .navigationTitle("Week \(ref.week), \(String(ref.year))")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button { step(by: -1, ctx.cal) } label: { Image(systemName: "chevron.left") }
                        .accessibilityLabel("Previous week")
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button { step(by: 1, ctx.cal) } label: { Image(systemName: "chevron.right") }
                        .accessibilityLabel("Next week")
                }
            }
            .simultaneousGesture(DragGesture(minimumDistance: 30).onEnded { v in
                guard abs(v.translation.width) > abs(v.translation.height) * 2 else { return }
                step(by: v.translation.width < 0 ? 1 : -1, ctx.cal)
            })
            .sensoryFeedback(.selection, trigger: ref)
            .sheet(isPresented: $addingMilestone) { MilestoneEditor(prefill: ref) }
        }
    }

    private func step(by weeks: Int, _ cal: Calendar) {
        if let next = cal.weekRef(ref, adding: weeks) { step(to: next) }
    }

    private func step(to next: WeekRef) {
        withAnimation(.snappy) { ref = next }
        copied = false
    }

    private func copy(_ text: String) {
        UIPasteboard.general.string = text
        UINotificationFeedbackGenerator().notificationOccurred(.success)
        copied = true
    }
}
