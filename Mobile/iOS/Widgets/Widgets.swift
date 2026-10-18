import SwiftUI
import WidgetKit

@main
struct WeekmarkWidgets: WidgetBundle {
    var body: some Widget {
        WeekWidget()
    }
}

struct WeekWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: "WeekmarkWeek", provider: WeekProvider()) { entry in
            WeekWidgetView(entry: entry)
        }
        .configurationDisplayName("Calendar Week")
        .description("The current calendar week, with milestones and sprints.")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge,
                            .accessoryCircular, .accessoryRectangular, .accessoryInline])
    }
}

struct WeekWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: WeekEntry

    var body: some View {
        switch family {
        case .accessoryCircular: CircularWeekView(entry: entry).containerBackground(for: .widget) { AccessoryWidgetBackground() }
        case .accessoryRectangular: RectangularWeekView(entry: entry).containerBackground(for: .widget) { Color.clear }
        case .accessoryInline: InlineWeekView(entry: entry).containerBackground(for: .widget) { Color.clear }
        case .systemMedium: MediumWeekView(entry: entry).containerBackground(for: .widget) { Color(.systemBackground) }
        case .systemLarge: LargeWeekView(entry: entry).containerBackground(for: .widget) { Color(.systemBackground) }
        default: SmallWeekView(entry: entry).containerBackground(for: .widget) { Color(.systemBackground) }
        }
    }
}

