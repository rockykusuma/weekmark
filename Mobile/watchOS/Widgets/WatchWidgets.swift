import SwiftUI
import WidgetKit

@main
struct WeekmarkWatchWidgets: WidgetBundle {
    var body: some Widget { WatchWeekWidget() }
}

struct WatchWeekWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: "WeekmarkWatchWeek", provider: WeekProvider()) { entry in
            WatchWidgetView(entry: entry)
        }
        .configurationDisplayName("Calendar Week")
        .description("The current calendar week.")
        .supportedFamilies([.accessoryCircular, .accessoryRectangular, .accessoryInline, .accessoryCorner])
    }
}

struct WatchWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: WeekEntry
    var body: some View {
        Group {
            switch family {
            case .accessoryRectangular: RectangularWeekView(entry: entry)
            case .accessoryInline: InlineWeekView(entry: entry)
            case .accessoryCorner:
                Text("\(entry.snap.weekNumber)")
                    .font(.system(.title2, design: .rounded).weight(.bold))
                    .widgetCurvesContent()
                    .widgetLabel { Text("CW · \(entry.snap.rangeText)") }
            default: CircularWeekView(entry: entry)
            }
        }
        .containerBackground(for: .widget) { Color.clear }
    }
}
