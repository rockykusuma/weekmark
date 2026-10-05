import SwiftUI
import CWCore

extension MilestoneColor {
    var color: Color {
        switch self {
        case .orange: return .orange
        case .red: return .red
        case .pink: return .pink
        case .purple: return .purple
        case .blue: return .blue
        case .teal: return .teal
        case .green: return .green
        }
    }
}

struct WidgetView: View {
    @ObservedObject var settings: Settings
    @ObservedObject var clock: Clock
    @ObservedObject var browser: Browser
    let context: () -> QueryContext
    @FocusState private var findFocused: Bool

    private let lookup = Color.orange
    private let holidayColor = Color.red

    var body: some View {
        let s = settings.size.scale
        let snap = WeekSnapshot(now: clock.now, numbering: settings.numbering)

        VStack(alignment: .leading, spacing: 14 * s) {
            header(snap, s)
            if settings.showPlanning { planning(snap, s) }
            weekStrip(snap, s)
            if settings.showProgress { progress(snap, s) }
            if settings.showMilestones, !upcoming(snap).isEmpty {
                milestones(snap, s).interactiveRegion("milestones")
            }
            Rectangle().fill(.primary.opacity(0.1)).frame(height: 1)
            if settings.showMonth {
                monthGrid(snap, s).interactiveRegion("month")
            }
            finder(snap, s).interactiveRegion("finder")
        }
        .padding(18 * s)
        .frame(width: 300 * s, alignment: .leading)
        .modifier(WidgetBackground(style: settings.background, radius: 24 * s))
        .padding(12) // room for the shadow
    }

    private func upcoming(_ snap: WeekSnapshot) -> [Milestone] {
        Array(Milestones.upcoming(settings.milestones, now: snap.now, cal: snap.cal).prefix(3))
    }

    // MARK: Header — big week number + year ring

    private func header(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        HStack(alignment: .top) {
            VStack(alignment: .leading, spacing: 0) {
                Text("CALENDAR WEEK")
                    .font(.system(size: 11 * s, weight: .semibold))
                    .tracking(1.6 * s)
                    .foregroundStyle(.secondary)
                Text(String(snap.weekNumber))
                    .font(.system(size: 78 * s, weight: .bold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(Color.accentColor)
                    .padding(.vertical, -6 * s)
                    .contentTransition(.numericText())
                Text(snap.rangeText)
                    .font(.system(size: 14 * s, weight: .medium))
                    .foregroundStyle(.secondary)
            }
            .accessibilityElement(children: .ignore)
            .accessibilityLabel("Calendar week \(snap.weekNumber), \(snap.rangeText)")
            Spacer(minLength: 8 * s)
            VStack(alignment: .trailing, spacing: 6 * s) {
                Text(String(snap.weekYear))
                    .font(.system(size: 13 * s, weight: .semibold, design: .rounded))
                    .foregroundStyle(.secondary)
                ZStack {
                    Circle().stroke(.primary.opacity(0.12), lineWidth: 5 * s)
                    Circle()
                        .trim(from: 0, to: snap.yearProgress)
                        .stroke(Color.accentColor, style: StrokeStyle(lineWidth: 5 * s, lineCap: .round))
                        .rotationEffect(.degrees(-90))
                    VStack(spacing: -1 * s) {
                        Text(String(snap.weekNumber))
                            .font(.system(size: 13 * s, weight: .bold, design: .rounded))
                        Text("of \(snap.weeksInYear)")
                            .font(.system(size: 8 * s, weight: .medium))
                            .foregroundStyle(.secondary)
                    }
                }
                .frame(width: 50 * s, height: 50 * s)
                Text("\(snap.weeksLeft) wks left")
                    .font(.system(size: 10 * s, weight: .medium))
                    .foregroundStyle(.secondary)
            }
            .accessibilityElement(children: .ignore)
            .accessibilityLabel("Week \(snap.weekNumber) of \(snap.weeksInYear), \(snap.weeksLeft) weeks left in \(snap.weekYear)")
        }
    }

    // MARK: Planning chips — quarter, sprint, PI

    @ViewBuilder
    private func planning(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        let q = Quarters.info(for: snap.now, config: settings.quarter, cal: snap.cal)
        let sp = Sprints.info(for: snap.now, config: settings.sprint, cal: snap.cal, holidays: settings.holidays)
        if q != nil || sp != nil {
            HStack(spacing: 6 * s) {
                if let q { chip(q.label, icon: "chart.pie", s) }
                if let sp {
                    chip("\(settings.sprint.name) \(sp.number) · day \(max(sp.workday, 1))/\(sp.workdaysTotal)", icon: "arrow.triangle.2.circlepath", s)
                        .help("CW\(sp.firstWeek.week)–CW\(sp.lastWeek.week)")
                    if let pi = sp.pi, let n = sp.sprintInPI {
                        chip("PI \(pi) · \(n)/\(sp.sprintsPerPI)", icon: "square.stack.3d.up", s)
                    }
                }
            }
            .lineLimit(1)
            .minimumScaleFactor(0.75)
        }
    }

    private func chip(_ text: String, icon: String, _ s: CGFloat) -> some View {
        HStack(spacing: 4 * s) {
            Image(systemName: icon).font(.system(size: 9 * s, weight: .semibold))
            Text(text).font(.system(size: 10.5 * s, weight: .semibold, design: .rounded))
        }
        .padding(.horizontal, 7 * s).padding(.vertical, 3.5 * s)
        .background(Capsule().fill(.primary.opacity(0.08)))
        .foregroundStyle(.secondary)
    }

    // MARK: 7-day strip

    private func weekStrip(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        HStack(spacing: 0) {
            ForEach(0..<7, id: \.self) { i in
                let d = snap.days[i]
                let today = snap.isToday(d)
                let hol = settings.holidays.holidays(on: d, cal: snap.cal)
                VStack(spacing: 5 * s) {
                    Text(snap.weekdaySymbol(d).uppercased())
                        .font(.system(size: 10 * s, weight: .semibold))
                        .foregroundStyle(today ? Color.accentColor : (snap.isWeekend(d) ? Color.secondary.opacity(0.7) : Color.secondary))
                    ZStack {
                        if today { Circle().fill(Color.accentColor) }
                        Text(snap.dayNumber(d))
                            .font(.system(size: 15 * s, weight: today ? .bold : .medium, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(today ? Color.white : !hol.isEmpty ? holidayColor : (snap.isPast(d) ? Color.secondary : Color.primary))
                    }
                    .frame(width: 30 * s, height: 30 * s)
                    Circle().fill(hol.isEmpty ? Color.clear : holidayColor).frame(width: 4 * s, height: 4 * s)
                }
                .frame(maxWidth: .infinity)
                .help(hol.map(\.name).joined(separator: ", "))
            }
        }
        .padding(.bottom, -6 * s)
    }

    // MARK: Week progress

    private func progress(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        VStack(alignment: .leading, spacing: 6 * s) {
            HStack {
                Text(snap.todayText)
                Spacer()
                Text("\(Int((snap.weekProgress * 100).rounded()))% of week")
                    .monospacedDigit()
            }
            .font(.system(size: 11 * s, weight: .medium))
            .foregroundStyle(.secondary)
            GeometryReader { g in
                ZStack(alignment: .leading) {
                    Capsule().fill(.primary.opacity(0.12))
                    Capsule().fill(Color.accentColor)
                        .frame(width: max(5 * s, g.size.width * snap.weekProgress))
                    HStack(spacing: 0) {
                        ForEach(0..<7, id: \.self) { i in
                            Rectangle().fill(Color.clear).frame(maxWidth: .infinity)
                            if i < 6 { Rectangle().fill(.background.opacity(0.6)).frame(width: 1) }
                        }
                    }
                }
            }
            .frame(height: 5 * s)
            .clipShape(Capsule())
        }
    }

    // MARK: Milestones

    private func milestones(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        VStack(alignment: .leading, spacing: 5 * s) {
            ForEach(upcoming(snap)) { m in
                let st = Milestones.status(m, now: snap.now, cal: snap.cal, holidays: settings.holidays)
                HStack(spacing: 7 * s) {
                    Image(systemName: "flag.fill")
                        .font(.system(size: 9 * s))
                        .foregroundStyle(m.color.color)
                    Text(m.name)
                        .font(.system(size: 12 * s, weight: .semibold))
                        .lineLimit(1)
                    Spacer(minLength: 4 * s)
                    Text("CW\(m.week.week)")
                        .font(.system(size: 11 * s, weight: .bold, design: .rounded))
                        .foregroundStyle(m.color.color)
                    if let st {
                        Text(st.weeksAway == 0 ? "\(st.workdaysLeft)d left" : "\(st.weeksAway)w · \(st.workdaysLeft)d")
                            .font(.system(size: 10.5 * s, weight: .medium, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(.secondary)
                    }
                }
                .contentShape(Rectangle())
                .onTapGesture { browser.select(m.week) }
                .help("\(m.name) — \(snap.fmt.longRange(m.week))")
            }
        }
    }

    // MARK: Month grid (scroll or ‹ › to browse, hover/click a week)

    private func monthGrid(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        let month = snap.displayedMonth(offset: browser.monthOffset)
        let rows = snap.monthRows(month: month)
        let cwWidth = 30 * s
        let msByWeek = Dictionary(grouping: settings.milestones, by: \.week)
        return VStack(spacing: 3 * s) {
            HStack(spacing: 6 * s) {
                Text(snap.monthTitle(month))
                    .font(.system(size: 12 * s, weight: .semibold))
                Spacer()
                if browser.isBrowsing {
                    Button("Today") { browser.reset(); findFocused = false }
                        .buttonStyle(PillButton(s: s))
                }
                Button { browser.step(-1) } label: { Image(systemName: "chevron.left") }
                    .buttonStyle(PillButton(s: s, square: true))
                    .accessibilityLabel("Previous month")
                Button { browser.step(1) } label: { Image(systemName: "chevron.right") }
                    .buttonStyle(PillButton(s: s, square: true))
                    .accessibilityLabel("Next month")
            }
            .padding(.bottom, 2 * s)
            HStack(spacing: 0) {
                Text("CW").frame(width: cwWidth)
                ForEach(0..<7, id: \.self) { i in
                    Text(snap.weekdaySymbol(snap.days[i], veryShort: true)).frame(maxWidth: .infinity)
                }
            }
            .font(.system(size: 9 * s, weight: .semibold))
            .foregroundStyle(.tertiary)

            ForEach(rows) { row in
                let isTarget = browser.target == row.ref
                let isHover = browser.hover == row.ref
                let ms = msByWeek[row.ref] ?? []
                let sprintStart = Sprints.startsSprint(row.ref, config: settings.sprint, cal: snap.cal)
                HStack(spacing: 0) {
                    HStack(spacing: 1.5 * s) {
                        if let m = ms.first {
                            Image(systemName: "flag.fill")
                                .font(.system(size: 6.5 * s))
                                .foregroundStyle(m.color.color)
                        }
                        Text(String(row.ref.week))
                            .font(.system(size: 10 * s, weight: .bold, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(isTarget ? lookup : row.isCurrent ? Color.accentColor : ms.first?.color.color ?? Color.secondary)
                    }
                    .frame(width: cwWidth)
                    .help(ms.map(\.name).joined(separator: ", "))
                    ForEach(0..<7, id: \.self) { i in
                        let d = row.days[i]
                        let today = snap.isToday(d)
                        let isHol = settings.holidays.isHoliday(Day(d, calendar: snap.cal))
                        let inMonth = snap.isIn(d, month: month)
                        Text(snap.dayNumber(d))
                            .font(.system(size: 11 * s, weight: today ? .bold : .regular, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(today ? AnyShapeStyle(Color.white)
                                             : !inMonth ? AnyShapeStyle(.quaternary)
                                             : isHol ? AnyShapeStyle(holidayColor) : AnyShapeStyle(.primary))
                            .frame(width: 21 * s, height: 21 * s)
                            .background { if today { Circle().fill(Color.accentColor) } }
                            .frame(maxWidth: .infinity)
                    }
                }
                .padding(.vertical, 1 * s)
                .background {
                    let shape = RoundedRectangle(cornerRadius: 7 * s, style: .continuous)
                    if isTarget {
                        shape.fill(lookup.opacity(0.2)).overlay(shape.strokeBorder(lookup.opacity(0.85), lineWidth: 1.2))
                    } else if row.isCurrent {
                        shape.fill(Color.accentColor.opacity(0.14))
                    } else if isHover {
                        shape.fill(.primary.opacity(0.07))
                    }
                }
                .overlay(alignment: .top) {
                    if sprintStart {
                        Rectangle().fill(Color.accentColor.opacity(0.45)).frame(height: 1).padding(.leading, cwWidth).offset(y: -2 * s)
                    }
                }
                .contentShape(Rectangle())
                .onHover { inside in
                    if inside { browser.hover = row.ref } else if browser.hover == row.ref { browser.hover = nil }
                }
                .onTapGesture { browser.select(row.ref) }
            }
        }
    }

    // MARK: Finder

    private func finder(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        VStack(alignment: .leading, spacing: 8 * s) {
            HStack(spacing: 6 * s) {
                Image(systemName: "magnifyingglass")
                    .font(.system(size: 11 * s, weight: .semibold))
                    .foregroundStyle(.secondary)
                TextField("Find CW", text: $browser.query, prompt: Text("46 · Dec 12 · +6w · 46-50"))
                    .textFieldStyle(.plain)
                    .font(.system(size: 12 * s, weight: .medium, design: .rounded))
                    .focused($findFocused)
                    .onExitCommand { browser.reset(); findFocused = false }
                if !browser.query.isEmpty {
                    Button { browser.reset(); findFocused = false } label: {
                        Image(systemName: "xmark.circle.fill").font(.system(size: 12 * s))
                    }
                    .buttonStyle(.plain)
                    .foregroundStyle(.tertiary)
                }
            }
            .padding(.horizontal, 10 * s)
            .padding(.vertical, 7 * s)
            .background(RoundedRectangle(cornerRadius: 9 * s, style: .continuous).fill(.primary.opacity(0.08)))

            result(snap, s)
                .frame(maxWidth: .infinity, minHeight: 32 * s, maxHeight: 32 * s, alignment: .topLeading)
        }
        .onChange(of: browser.focusToken) { _, _ in findFocused = true }
    }

    @ViewBuilder
    private func result(_ snap: WeekSnapshot, _ s: CGFloat) -> some View {
        let r: QueryResult? = browser.hover.map { QueryEngine.weekResult($0, context()) } ?? browser.result
        if let r {
            let color: Color = r.kind == .error ? .red
                : r.week == browser.target && browser.hover == nil ? lookup
                : r.week == snap.current ? Color.accentColor : Color.primary
            VStack(alignment: .leading, spacing: 2 * s) {
                Text(r.title)
                    .font(.system(size: 12 * s, weight: .semibold))
                    .foregroundStyle(color)
                    .lineLimit(1)
                    .minimumScaleFactor(0.75)
                Text(r.detail.map { "\(r.subtitle) · \($0)" } ?? r.subtitle)
                    .font(.system(size: 10.5 * s))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .truncationMode(.tail)
            }
        } else {
            Text("Type a week, date or offset — or press \(settings.hotkey == .off ? "the menu bar icon" : settings.hotkey.title) anywhere.")
                .font(.system(size: 10.5 * s))
                .foregroundStyle(.tertiary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}

struct PillButton: ButtonStyle {
    let s: CGFloat
    var square = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 10 * s, weight: .semibold))
            .padding(.horizontal, square ? 0 : 8 * s)
            .frame(width: square ? 20 * s : nil, height: 20 * s)
            .background(Capsule().fill(.primary.opacity(configuration.isPressed ? 0.18 : 0.09)))
            .contentShape(Capsule())
    }
}

// MARK: - Hit regions (areas that take clicks instead of dragging the widget)

final class HitRegions {
    static let shared = HitRegions()
    var rects: [String: CGRect] = [:]
    func contains(_ p: CGPoint) -> Bool { rects.values.contains { $0.contains(p) } }
    func contains(_ p: CGPoint, id: String) -> Bool { rects[id]?.contains(p) ?? false }
}

extension View {
    func interactiveRegion(_ id: String) -> some View {
        background(GeometryReader { g in
            Color.clear
                .onAppear { HitRegions.shared.rects[id] = g.frame(in: .global) }
                .onChange(of: g.frame(in: .global)) { _, f in HitRegions.shared.rects[id] = f }
                .onDisappear { HitRegions.shared.rects[id] = nil }
        })
    }
}

// MARK: - Background

struct WidgetBackground: ViewModifier {
    let style: BackgroundStyle
    let radius: CGFloat
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency

    func body(content: Content) -> some View {
        let shape = RoundedRectangle(cornerRadius: radius, style: .continuous)
        switch reduceTransparency ? .solid : style {
        case .glass:
            if #available(macOS 26.0, *) {
                content
                    .background(shape.fill(Color(nsColor: .windowBackgroundColor).opacity(0.45)))
                    .glassEffect(.regular, in: shape)
            } else {
                content
                    .background(shape.fill(Color(nsColor: .windowBackgroundColor).opacity(0.35)))
                    .background(VisualEffect().clipShape(shape))
                    .overlay(shape.strokeBorder(.white.opacity(0.12)))
            }
        case .solid:
            content
                .background(shape.fill(Color(nsColor: .windowBackgroundColor)))
                .overlay(shape.strokeBorder(.primary.opacity(0.08)))
                .shadow(color: .black.opacity(0.25), radius: 8, y: 3)
        case .clear:
            content.shadow(color: .black.opacity(0.35), radius: 3, y: 1)
        }
    }
}

struct VisualEffect: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView {
        let v = NSVisualEffectView()
        v.material = .popover
        v.blendingMode = .behindWindow
        v.state = .active
        return v
    }
    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {}
}
