import AppKit
import SwiftUI
import CWCore

/// State of the Year at a Glance window; AppDelegate keeps it so reopening the window starts at this year again.
final class YearModel: ObservableObject {
    @Published var year: Int
    @Published var hover: WeekRef?
    @Published var pinned: WeekRef?
    init(year: Int) { self.year = year }
}

/// A calendar year as 12 month grids with a week-number column; hover a week for its details, click to pin it.
struct YearView: View {
    @ObservedObject var settings: Settings
    @ObservedObject var clock: Clock
    @ObservedObject var model: YearModel
    let context: () -> QueryContext
    @FocusState private var focused: Bool

    private let lookup = Color.orange
    private let holidayColor = Color.red
    private let cwWidth: CGFloat = 30

    var body: some View {
        let snap = WeekSnapshot(now: clock.now, numbering: settings.numbering)
        let msByWeek = Dictionary(grouping: settings.milestones, by: \.week)
        VStack(spacing: 0) {
            header(snap)
            ScrollView {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 220, maximum: 340), spacing: 14)], spacing: 14) {
                    ForEach(1...12, id: \.self) { m in month(m, snap, msByWeek) }
                }
                .padding(.horizontal, 20)
                .padding(.bottom, 18)
            }
            Divider()
            footer(snap)
        }
        .frame(minWidth: 520, minHeight: 440)
        .focusable()
        .focusEffectDisabled()
        .focused($focused)
        .onAppear { focused = true }
        .onKeyPress(.leftArrow) { model.year -= 1; return .handled }
        .onKeyPress(.rightArrow) { model.year += 1; return .handled }
        .onExitCommand { NSApp.keyWindow?.performClose(nil) }
    }

    // MARK: Header — year, week count, ‹ ›

    private func header(_ snap: WeekSnapshot) -> some View {
        let thisYear = snap.cal.component(.year, from: snap.now)
        var subtitle = "\(snap.cal.weeksIn(weekYear: model.year)) calendar weeks"
        if model.year == snap.weekYear { subtitle += " · week \(snap.weekNumber) now, \(snap.weeksLeft) to go" }
        return HStack(alignment: .firstTextBaseline, spacing: 12) {
            Text(String(model.year))
                .font(.system(size: 34, weight: .bold, design: .rounded))
                .monospacedDigit()
                .contentTransition(.numericText())
            Text(subtitle)
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.secondary)
            Spacer()
            if model.year != thisYear {
                Button("This Year") { model.year = thisYear }
                    .buttonStyle(PillButton(s: 1.25))
            }
            Button { model.year -= 1 } label: { Image(systemName: "chevron.left") }
                .buttonStyle(PillButton(s: 1.25, square: true))
                .help("Previous year (←)")
            Button { model.year += 1 } label: { Image(systemName: "chevron.right") }
                .buttonStyle(PillButton(s: 1.25, square: true))
                .help("Next year (→)")
        }
        .padding(.horizontal, 22)
        .padding(.top, 16)
        .padding(.bottom, 12)
    }

    // MARK: Month — same marks as the widget's month grid

    private func month(_ m: Int, _ snap: WeekSnapshot, _ msByWeek: [WeekRef: [Milestone]]) -> some View {
        let month = snap.cal.date(from: DateComponents(year: model.year, month: m, day: 1))!
        return VStack(spacing: 2) {
            Text(snap.fmt.format(month, "MMMM"))
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(snap.isIn(snap.now, month: month) ? Color.accentColor : Color.primary)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.leading, 4)
                .padding(.bottom, 4)
            HStack(spacing: 0) {
                Text("CW").frame(width: cwWidth)
                ForEach(0..<7, id: \.self) { i in
                    Text(snap.weekdaySymbol(snap.days[i], veryShort: true)).frame(maxWidth: .infinity)
                }
            }
            .font(.system(size: 9, weight: .semibold))
            .foregroundStyle(.tertiary)
            // monthRows is always 6 rows, so every month card has the same height.
            ForEach(snap.monthRows(month: month)) { row in
                if row.days.contains(where: { snap.isIn($0, month: month) }) {
                    weekRow(row, month, snap, msByWeek[row.ref] ?? [])
                } else {
                    Color.clear.frame(height: 24)
                }
            }
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(.primary.opacity(0.045)))
    }

    private func weekRow(_ row: MonthRow, _ month: Date, _ snap: WeekSnapshot, _ ms: [Milestone]) -> some View {
        let isPinned = model.pinned == row.ref
        let isHover = model.hover == row.ref
        let sprintStart = Sprints.startsSprint(row.ref, config: settings.sprint, cal: snap.cal)
        return HStack(spacing: 0) {
            HStack(spacing: 1.5) {
                if let m = ms.first {
                    Image(systemName: "flag.fill")
                        .font(.system(size: 6.5))
                        .foregroundStyle(m.color.color)
                }
                Text(String(row.ref.week))
                    .font(.system(size: 10, weight: .bold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(isPinned ? lookup : row.isCurrent ? Color.accentColor : ms.first?.color.color ?? Color.secondary)
            }
            .frame(width: cwWidth)
            .help(ms.map(\.name).joined(separator: ", "))
            ForEach(0..<7, id: \.self) { i in
                let d = row.days[i]
                if snap.isIn(d, month: month) {
                    let today = snap.isToday(d)
                    let hol = settings.holidays.holidays(on: d, cal: snap.cal)
                    Text(snap.dayNumber(d))
                        .font(.system(size: 11, weight: today ? .bold : .regular, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(today ? AnyShapeStyle(Color.white)
                                         : !hol.isEmpty ? AnyShapeStyle(holidayColor)
                                         : snap.isPast(d) ? AnyShapeStyle(.secondary) : AnyShapeStyle(.primary))
                        .frame(width: 21, height: 21)
                        .background { if today { Circle().fill(Color.accentColor) } }
                        .frame(maxWidth: .infinity)
                        .help(hol.map(\.name).joined(separator: ", "))
                } else {
                    Color.clear.frame(maxWidth: .infinity, maxHeight: 21)
                }
            }
        }
        .padding(.vertical, 1.5)
        .background {
            let shape = RoundedRectangle(cornerRadius: 7, style: .continuous)
            if isPinned {
                shape.fill(lookup.opacity(0.2)).overlay(shape.strokeBorder(lookup.opacity(0.85), lineWidth: 1.2))
            } else if row.isCurrent {
                shape.fill(Color.accentColor.opacity(0.14))
            } else if isHover {
                shape.fill(.primary.opacity(0.07))
            }
        }
        .overlay(alignment: .top) {
            if sprintStart {
                Rectangle().fill(Color.accentColor.opacity(0.45)).frame(height: 1).padding(.leading, cwWidth).offset(y: -1)
            }
        }
        .contentShape(Rectangle())
        .onHover { inside in
            if inside { model.hover = row.ref } else if model.hover == row.ref { model.hover = nil }
        }
        .onTapGesture { model.pinned = isPinned ? nil : row.ref }
    }

    // MARK: Footer — details of the hovered, pinned or current week

    private func footer(_ snap: WeekSnapshot) -> some View {
        let ref = model.hover ?? model.pinned ?? snap.current
        let r = QueryEngine.weekResult(ref, context())
        let copy = r.copyText ?? snap.fmt.copyText(ref, currentYear: snap.weekYear)
        return HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text(r.title)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(ref == model.pinned ? lookup : ref == snap.current ? Color.accentColor : Color.primary)
                    .lineLimit(1)
                Text(r.detail.map { "\(r.subtitle) · \($0)" } ?? r.subtitle)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .truncationMode(.tail)
            }
            Spacer(minLength: 8)
            Button("Copy “\(copy)”") {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(copy, forType: .string)
            }
            .keyboardShortcut("c", modifiers: .command)
        }
        .padding(.horizontal, 22)
        .padding(.vertical, 12)
    }
}
