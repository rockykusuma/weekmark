import SwiftUI
import WidgetKit

#if DEBUG
/// Debug-only launch options:
///   -demoData       seed sample milestones and sprints (for screenshots)
///   -widgetGallery  show every widget size on one screen
enum DebugLaunch {
    static var args: [String] { ProcessInfo.processInfo.arguments }
    static var widgetGallery: Bool { args.contains("-widgetGallery") }
    static var tab: Int? { args.firstIndex(of: "-tab").flatMap { i in i + 1 < args.count ? Int(args[i + 1]) : nil } }

    @MainActor static func applyDemoData(_ store: PlanStore) {
        guard args.contains("-demoData") else { return }
        let cal = store.calendar
        let cur = cal.weekRef(for: Date())
        store.milestones = [
            Milestone(name: "Code freeze", week: cal.weekRef(cur, adding: 3)!, color: .purple),
            Milestone(name: "Release 5.0", week: cal.weekRef(cur, adding: 5)!, color: .orange),
        ]
        var sp = SprintConfig()
        sp.enabled = true; sp.lengthWeeks = 2; sp.anchor = Day(2026, 1, 5); sp.sprintsPerPI = 5
        store.sprint = sp
    }
}

struct WidgetGalleryView: View {
    @EnvironmentObject private var store: PlanStore
    var body: some View {
        let entry = WeekEntry(date: Date(), plan: store.snapshot)
        ScrollView {
            VStack(spacing: 18) {
                HStack(spacing: 14) {
                    tile(170, 170) { SmallWeekView(entry: entry) }
                    VStack(spacing: 10) {
                        accessory(72, 72) { CircularWeekView(entry: entry) }
                        accessory(160, 72) { RectangularWeekView(entry: entry) }
                    }
                }
                accessory(300, 24) { InlineWeekView(entry: entry) }
                tile(364, 170) { MediumWeekView(entry: entry) }
                tile(364, 382) { LargeWeekView(entry: entry) }
            }
            .padding()
        }
        .background(LinearGradient(colors: [.indigo, .teal], startPoint: .topLeading, endPoint: .bottomTrailing).ignoresSafeArea())
    }

    private func tile<C: View>(_ w: CGFloat, _ h: CGFloat, @ViewBuilder _ c: () -> C) -> some View {
        c().padding(16).frame(width: w, height: h)
            .background(RoundedRectangle(cornerRadius: 22, style: .continuous).fill(Color(.systemBackground)))
    }
    private func accessory<C: View>(_ w: CGFloat, _ h: CGFloat, @ViewBuilder _ c: () -> C) -> some View {
        c().frame(width: w, height: h).foregroundStyle(.white).environment(\.colorScheme, .dark)
    }
}
#endif
