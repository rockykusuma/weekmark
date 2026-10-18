import SwiftUI

@main
struct WeekmarkMobileApp: App {
    @StateObject private var store = PlanStore.shared
    @Environment(\.scenePhase) private var phase

    var body: some Scene {
        WindowGroup {
            Group {
                #if DEBUG
                if DebugLaunch.widgetGallery { WidgetGalleryView() } else { RootView() }
                #else
                RootView()
                #endif
            }
                .environmentObject(store)
                .tint(Color.accentColor)
                .onChange(of: phase) { _, p in if p == .active { store.syncNow() } }
                #if DEBUG
                .task { DebugLaunch.applyDemoData(store) }
                #endif
        }
    }
}

struct RootView: View {
    #if DEBUG
    @State private var tab = DebugLaunch.tab ?? 0
    #else
    @State private var tab = 0
    #endif
    var body: some View {
        TabView(selection: $tab) {
            WeekTab().tabItem { Label("Week", systemImage: "calendar") }.tag(0)
            FindTab().tabItem { Label("Find", systemImage: "magnifyingglass") }.tag(1)
            SettingsTab().tabItem { Label("Settings", systemImage: "gearshape") }.tag(2)
        }
    }
}

extension WeekRef: Identifiable {
    public var id: String { "\(year)-\(week)" }
}
