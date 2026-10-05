import SwiftUI
import AppKit
import ServiceManagement
import UniformTypeIdentifiers
import CWCore

enum SettingsTab: String, CaseIterable, Hashable {
    case general, widget, planning, holidays, milestones, about
}

/// Routes the settings window to a tab (and pre-fills a milestone week).
final class SettingsRouter: ObservableObject {
    @Published var tab: SettingsTab = .general
    @Published var prefillWeek: WeekRef?
}

struct SettingsView: View {
    @ObservedObject var settings: Settings
    @ObservedObject var router: SettingsRouter

    var body: some View {
        TabView(selection: $router.tab) {
            GeneralPane(settings: settings).tabItem { Label("General", systemImage: "gearshape") }.tag(SettingsTab.general)
            WidgetPane(settings: settings).tabItem { Label("Widget", systemImage: "rectangle.on.rectangle") }.tag(SettingsTab.widget)
            PlanningPane(settings: settings).tabItem { Label("Planning", systemImage: "arrow.triangle.2.circlepath") }.tag(SettingsTab.planning)
            HolidaysPane(settings: settings).tabItem { Label("Holidays", systemImage: "sun.max") }.tag(SettingsTab.holidays)
            MilestonesPane(settings: settings, router: router).tabItem { Label("Milestones", systemImage: "flag") }.tag(SettingsTab.milestones)
            AboutPane().tabItem { Label("About", systemImage: "info.circle") }.tag(SettingsTab.about)
        }
        .frame(width: 560, height: 520)
    }
}

// MARK: General

struct GeneralPane: View {
    @ObservedObject var settings: Settings
    @ObservedObject private var updater = Updater.shared
    @State private var loginEnabled = SMAppService.mainApp.status == .enabled
    @State private var loginError: String?

    var body: some View {
        Form {
            Section {
                Picker("Week numbering", selection: $settings.numbering) {
                    ForEach(WeekNumbering.allCases, id: \.self) { Text($0.title).tag($0) }
                }
                Text("ISO 8601 is the standard in Europe and most engineering orgs. US weeks start on Sunday and week 1 contains January 1.")
                    .font(.caption).foregroundStyle(.secondary)
            }
            Section {
                Picker("Quick Finder hotkey", selection: $settings.hotkey) {
                    ForEach(HotkeyPreset.allCases, id: \.self) { Text($0.title).tag($0) }
                }
                Text("Press it from any app to look up a week, date, range or countdown, then press ⏎ to copy.")
                    .font(.caption).foregroundStyle(.secondary)
            }
            Section {
                Toggle("Launch at login", isOn: Binding(get: { loginEnabled }, set: { setLogin($0) }))
                if let loginError { Text(loginError).font(.caption).foregroundStyle(.red) }
                Toggle("Show week number in menu bar", isOn: $settings.showInMenuBar)
            }
            Section("Updates") {
                Toggle("Check for updates automatically", isOn: $updater.automaticChecks)
                HStack {
                    Button("Check Now") { updater.checkForUpdates() }
                    Spacer()
                    if let last = updater.lastCheck {
                        Text("Last checked \(last.formatted(.relative(presentation: .named)))").font(.caption).foregroundStyle(.secondary)
                    }
                }
            }
            Section {
                Button("Show Welcome Screen Again") { NotificationCenter.default.post(name: .showOnboarding, object: nil) }
            }
        }
        .formStyle(.grouped)
    }

    private func setLogin(_ on: Bool) {
        do {
            if on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
            loginError = nil
        } catch {
            loginError = "\(error.localizedDescription) Move the app to /Applications and try again."
        }
        loginEnabled = SMAppService.mainApp.status == .enabled
    }
}

// MARK: Widget

struct WidgetPane: View {
    @ObservedObject var settings: Settings
    var body: some View {
        Form {
            Section("Placement") {
                Picker("Layer", selection: $settings.layer) {
                    ForEach(WindowLayer.allCases, id: \.self) { Text($0.title).tag($0) }
                }
                Toggle("Lock position", isOn: $settings.locked)
                Text("Drag the top half of the widget to move it. Its position is remembered per display.")
                    .font(.caption).foregroundStyle(.secondary)
            }
            Section("Look") {
                Picker("Size", selection: $settings.size) {
                    ForEach(WidgetSize.allCases, id: \.self) { Text($0.title).tag($0) }
                }
                Picker("Style", selection: $settings.background) {
                    ForEach(BackgroundStyle.allCases, id: \.self) { Text($0.title).tag($0) }
                }
                Picker("Appearance", selection: $settings.appearance) {
                    ForEach(AppearanceMode.allCases, id: \.self) { Text($0.title).tag($0) }
                }
            }
            Section("Sections") {
                Toggle("Quarter & sprint chips", isOn: $settings.showPlanning)
                Toggle("Week progress", isOn: $settings.showProgress)
                Toggle("Upcoming milestones", isOn: $settings.showMilestones)
                Toggle("Month calendar", isOn: $settings.showMonth)
            }
        }
        .formStyle(.grouped)
    }
}

// MARK: Planning

struct PlanningPane: View {
    @ObservedObject var settings: Settings
    private let months = Calendar(identifier: .gregorian).monthSymbols

    var body: some View {
        Form {
            Section("Sprints") {
                Toggle("Show sprints", isOn: $settings.sprint.enabled)
                if settings.sprint.enabled {
                    TextField("Name", text: $settings.sprint.name)
                    Stepper("Length: \(settings.sprint.lengthWeeks) week\(settings.sprint.lengthWeeks == 1 ? "" : "s")",
                            value: $settings.sprint.lengthWeeks, in: 1...6)
                    DatePicker("A sprint started on", selection: anchorBinding, displayedComponents: .date)
                    Stepper("That sprint's number: \(settings.sprint.firstNumber)", value: $settings.sprint.firstNumber, in: 0...9999)
                    Text(preview).font(.caption).foregroundStyle(.secondary)
                }
            }
            if settings.sprint.enabled {
                Section("Program Increments (SAFe)") {
                    Stepper(settings.sprint.sprintsPerPI == 0 ? "Off" : "\(settings.sprint.sprintsPerPI) sprints per PI",
                            value: $settings.sprint.sprintsPerPI, in: 0...12)
                    if settings.sprint.sprintsPerPI > 0 {
                        Stepper("That sprint is in PI \(settings.sprint.firstPI)", value: $settings.sprint.firstPI, in: 0...999)
                    }
                }
            }
            Section("Quarters") {
                Toggle("Show quarter", isOn: $settings.quarter.enabled)
                if settings.quarter.enabled {
                    Picker("Quarters", selection: $settings.quarter.mode) {
                        ForEach(QuarterMode.allCases, id: \.self) { Text($0.title).tag($0) }
                    }
                    if settings.quarter.mode == .months {
                        Picker("Fiscal year starts in", selection: $settings.quarter.fiscalStartMonth) {
                            ForEach(1...12, id: \.self) { Text(months[$0 - 1]).tag($0) }
                        }
                    }
                }
            }
        }
        .formStyle(.grouped)
    }

    private var cal: Calendar { makeCalendar(settings.numbering) }

    private var anchorBinding: Binding<Date> {
        Binding(get: { settings.sprint.anchor.date(in: cal) ?? Date() },
                set: { settings.sprint.anchor = Day($0, calendar: cal) })
    }

    private var preview: String {
        guard let i = Sprints.info(for: Date(), config: settings.sprint, cal: cal, holidays: settings.holidays) else {
            return "Pick any past sprint's start date and number; the app counts forward from there."
        }
        var s = "Now: \(settings.sprint.name) \(i.number) (CW\(i.firstWeek.week)–CW\(i.lastWeek.week)), working day \(max(i.workday, 1)) of \(i.workdaysTotal)"
        if let pi = i.pi, let n = i.sprintInPI { s += " · PI \(pi), sprint \(n) of \(i.sprintsPerPI)" }
        return s
    }
}

// MARK: Holidays

struct HolidaysPane: View {
    @ObservedObject var settings: Settings
    @State private var newName = ""
    @State private var newDate = Date()

    var body: some View {
        Form {
            Section("Public holidays") {
                ForEach(HolidayRegion.allCases, id: \.self) { r in
                    Toggle("\(r.flag)  \(r.title)", isOn: Binding(
                        get: { settings.regions.contains(r) },
                        set: { on in if on { settings.regions.insert(r) } else { settings.regions.remove(r) } }))
                }
            }
            Section("Company holidays") {
                HStack {
                    Button("Import .ics File…") { importICS() }
                    Spacer()
                    if !settings.customHolidays.isEmpty {
                        Button("Remove All", role: .destructive) { settings.customHolidays = [] }
                    }
                }
                Text("Export your company's holiday calendar from Outlook or Google Calendar as .ics. Festivals like Diwali or Eid can't be calculated, so add them this way.")
                    .font(.caption).foregroundStyle(.secondary)
                HStack {
                    TextField("Name", text: $newName)
                    DatePicker("", selection: $newDate, displayedComponents: .date).labelsHidden()
                    Button("Add") {
                        let cal = makeCalendar(settings.numbering)
                        settings.customHolidays.append(Holiday(day: Day(newDate, calendar: cal),
                                                               name: newName.isEmpty ? "Holiday" : newName, source: "custom"))
                        newName = ""
                    }
                }
                ForEach(groupedSources, id: \.0) { source, count in
                    HStack {
                        Text(source == "custom" ? "Added by you" : source)
                        Spacer()
                        Text("\(count) day\(count == 1 ? "" : "s")").foregroundStyle(.secondary)
                        Button(role: .destructive) { settings.customHolidays.removeAll { $0.source == source } } label: {
                            Image(systemName: "trash")
                        }.buttonStyle(.borderless)
                    }
                }
            }
            Section("Coming up") {
                let cal = makeCalendar(settings.numbering)
                let list = settings.holidays.upcoming(from: Date(), cal: cal, limit: 6)
                if list.isEmpty { Text("No holidays selected").foregroundStyle(.secondary) }
                ForEach(list, id: \.self) { h in
                    HStack {
                        Text(h.name)
                        Spacer()
                        if let d = h.day.date(in: cal) {
                            Text("\(WeekFormatter(cal: cal).format(d, "EEEdMMMyyyy")) · CW\(cal.weekRef(for: d).week)")
                                .foregroundStyle(.secondary).monospacedDigit()
                        }
                    }
                }
            }
        }
        .formStyle(.grouped)
    }

    private var groupedSources: [(String, Int)] {
        Dictionary(grouping: settings.customHolidays, by: \.source).map { ($0.key, $0.value.count) }.sorted { $0.0 < $1.0 }
    }

    private func importICS() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "ics") ?? .data]
        panel.allowsMultipleSelection = true
        NSApp.activate(ignoringOtherApps: true)
        guard panel.runModal() == .OK else { return }
        for url in panel.urls {
            guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
            let name = url.deletingPathExtension().lastPathComponent
            settings.customHolidays.removeAll { $0.source == name }
            settings.customHolidays += ICSParser.parse(text, source: name)
        }
    }
}

// MARK: Milestones

struct MilestonesPane: View {
    @ObservedObject var settings: Settings
    @ObservedObject var router: SettingsRouter
    @State private var name = ""
    @State private var weekText = ""
    @State private var color: MilestoneColor = .orange
    @FocusState private var nameFocused: Bool

    var body: some View {
        let ctx = QueryContext(cal: makeCalendar(settings.numbering), now: Date(), numbering: settings.numbering,
                               holidays: settings.holidays)
        let parsed = QueryEngine.parseWeek(weekText, ctx)
        Form {
            Section("New milestone") {
                TextField("Name (e.g. Release 5.0, Code freeze)", text: $name).focused($nameFocused)
                HStack {
                    TextField("Week (e.g. 46, CW3 2027)", text: $weekText)
                    switch parsed {
                    case .week(let ref): Text(WeekFormatter(cal: ctx.cal).longRange(ref)).foregroundStyle(.secondary).font(.caption)
                    case .invalid(let m): Text(m).foregroundStyle(.red).font(.caption)
                    case .none: EmptyView()
                    }
                }
                Picker("Color", selection: $color) {
                    ForEach(MilestoneColor.allCases, id: \.self) { c in
                        Label(c.rawValue.capitalized, systemImage: "flag.fill").tint(c.color).tag(c)
                    }
                }
                Button("Add Milestone") {
                    guard case .week(let ref) = parsed, !name.trimmingCharacters(in: .whitespaces).isEmpty else { return }
                    settings.milestones.append(Milestone(name: name.trimmingCharacters(in: .whitespaces), week: ref, color: color))
                    settings.milestones.sort { $0.week < $1.week }
                    name = ""; weekText = ""
                }
                .disabled({ if case .week = parsed { return name.trimmingCharacters(in: .whitespaces).isEmpty }; return true }())
                .keyboardShortcut(.defaultAction)
            }
            Section("Milestones") {
                if settings.milestones.isEmpty {
                    Text("None yet. Tip: right-click a week in the widget to add one.").foregroundStyle(.secondary)
                }
                ForEach(settings.milestones) { m in
                    let st = Milestones.status(m, now: Date(), cal: ctx.cal, holidays: settings.holidays)
                    HStack {
                        Image(systemName: "flag.fill").foregroundStyle(m.color.color)
                        VStack(alignment: .leading) {
                            Text(m.name)
                            Text("CW\(m.week.week) \(m.week.year) · \(WeekFormatter(cal: ctx.cal).shortRange(m.week))")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        if let st {
                            Text(st.isPast ? "Done" : "\(st.weeksAway)w · \(st.workdaysLeft) working days")
                                .font(.caption).foregroundStyle(.secondary).monospacedDigit()
                        }
                        Button(role: .destructive) { settings.milestones.removeAll { $0.id == m.id } } label: {
                            Image(systemName: "trash")
                        }.buttonStyle(.borderless)
                    }
                }
            }
        }
        .formStyle(.grouped)
        .onAppear(perform: applyPrefill)
        .onChange(of: router.prefillWeek) { _, _ in applyPrefill() }
    }

    private func applyPrefill() {
        guard let ref = router.prefillWeek else { return }
        weekText = "CW\(ref.week) \(ref.year)"
        router.prefillWeek = nil
        nameFocused = true
    }
}

// MARK: About

struct AboutPane: View {
    var body: some View {
        VStack(spacing: 10) {
            Image(nsImage: NSApp.applicationIconImage).resizable().frame(width: 96, height: 96)
            Text("Weekmark").font(.title2.bold())
            Text("Version \(Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev")")
                .foregroundStyle(.secondary)
            Text("Any calendar week, one keystroke away.\nRuns on your Mac. No account, no tracking.\nThe only network request is the optional update check.")
                .multilineTextAlignment(.center).foregroundStyle(.secondary).font(.callout)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

extension Notification.Name {
    static let showOnboarding = Notification.Name("CWShowOnboarding")
}

// MARK: - Onboarding

struct OnboardingView: View {
    @ObservedObject var settings: Settings
    let onFinish: () -> Void
    @State private var step = 0
    @State private var launchAtLogin = true

    var body: some View {
        let snap = WeekSnapshot(now: Date(), numbering: settings.numbering)
        VStack(spacing: 0) {
            Group {
                switch step {
                case 0: welcome(snap)
                case 1: setup
                default: finish
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .padding(32)

            HStack {
                HStack(spacing: 6) {
                    ForEach(0..<3) { i in Circle().fill(i == step ? Color.accentColor : .secondary.opacity(0.3)).frame(width: 7, height: 7) }
                }
                Spacer()
                if step > 0 { Button("Back") { step -= 1 } }
                Button(step == 2 ? "Start" : "Continue") {
                    if step < 2 { step += 1 } else { finishOnboarding() }
                }
                .keyboardShortcut(.defaultAction)
                .buttonStyle(.borderedProminent)
            }
            .padding(20)
        }
        .frame(width: 520, height: 480)
    }

    private func welcome(_ snap: WeekSnapshot) -> some View {
        VStack(spacing: 14) {
            Text("This is").font(.title3).foregroundStyle(.secondary)
            Text("CW\(snap.weekNumber)")
                .font(.system(size: 96, weight: .bold, design: .rounded))
                .foregroundStyle(Color.accentColor)
            Text(snap.rangeText + ", \(snap.weekYear)").font(.title3.weight(.medium))
            Text("Weekmark keeps the current week on your desktop and answers “when is CW46?” from anywhere on your Mac.")
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
                .padding(.top, 8)
        }
    }

    private var setup: some View {
        Form {
            Picker("Week numbering", selection: $settings.numbering) {
                ForEach(WeekNumbering.allCases, id: \.self) { Text($0.title).tag($0) }
            }
            Picker("Public holidays", selection: Binding(
                get: { settings.regions.first },
                set: { r in settings.regions = r.map { [$0] } ?? [] })) {
                Text("None").tag(HolidayRegion?.none)
                ForEach(HolidayRegion.allCases, id: \.self) { Text("\($0.flag)  \($0.title)").tag(HolidayRegion?.some($0)) }
            }
            Picker("Quick Finder hotkey", selection: $settings.hotkey) {
                ForEach(HotkeyPreset.allCases, id: \.self) { Text($0.title).tag($0) }
            }
            Text("Holidays are used for working-day counts. You can add more countries, your company's holiday calendar, sprints and milestones later in Settings.")
                .font(.caption).foregroundStyle(.secondary)
        }
        .formStyle(.grouped)
    }

    private var finish: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("You're set").font(.title.bold())
            tip("hand.draw", "Drag the widget onto any display. It remembers where you put it.")
            tip("keyboard", settings.hotkey == .off ? "Open the Quick Finder from the menu bar." : "Press \(settings.hotkey.title) anywhere and type 46, Dec 12, +6w or 46-50. Press ⏎ to copy.")
            tip("cursorarrow.rays", "Hover a week in the widget to see its dates. Right-click it to add a milestone.")
            tip("gearshape", "Set up sprints, quarters, holidays and milestones in Settings.")
            Toggle("Launch Weekmark at login", isOn: $launchAtLogin).padding(.top, 6)
        }
    }

    private func tip(_ icon: String, _ text: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: icon).font(.title3).foregroundStyle(Color.accentColor).frame(width: 28)
            Text(text).fixedSize(horizontal: false, vertical: true)
        }
    }

    private func finishOnboarding() {
        if launchAtLogin, SMAppService.mainApp.status != .enabled { try? SMAppService.mainApp.register() }
        settings.onboardingDone = true
        onFinish()
    }
}
