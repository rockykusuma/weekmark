import SwiftUI
import UniformTypeIdentifiers

struct SettingsTab: View {
    @EnvironmentObject private var store: PlanStore
    @State private var addingMilestone = false
    private let months = Calendar(identifier: .gregorian).monthSymbols

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    HStack(spacing: 12) {
                        Image(systemName: store.iCloudAvailable ? "icloud.fill" : "icloud.slash")
                            .foregroundStyle(store.iCloudAvailable ? Color.accentColor : .secondary)
                            .font(.title3)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(store.iCloudAvailable ? "Syncing with iCloud" : "iCloud is off")
                                .font(.subheadline.weight(.semibold))
                            Text(store.iCloudAvailable
                                 ? "Milestones, sprints, quarters, holidays and week numbering stay the same on your Mac, iPhone and Apple Watch."
                                 : "Sign in to iCloud in Settings to share milestones and sprints with your Mac.")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                    }
                    if store.iCloudAvailable {
                        Button("Sync Now") { store.syncNow() }
                        if let t = store.lastSync {
                            LabeledContent("Last synced", value: t.formatted(.relative(presentation: .named)))
                        }
                    }
                }

                Section {
                    Picker("Week numbering", selection: $store.numbering) {
                        ForEach(WeekNumbering.allCases, id: \.self) { Text($0.title).tag($0) }
                    }
                } footer: {
                    Text("ISO 8601 is the standard in Europe and most engineering teams. US weeks start on Sunday.")
                }

                Section("Milestones") {
                    if store.milestones.isEmpty {
                        Text("None yet. Add one, or open any week and tap Add Milestone.").foregroundStyle(.secondary)
                    }
                    ForEach(store.milestones) { m in
                        let st = Milestones.status(m, now: Date(), cal: store.calendar, holidays: store.holidays)
                        HStack {
                            Image(systemName: "flag.fill").foregroundStyle(m.color.color)
                            VStack(alignment: .leading) {
                                Text(m.name)
                                Text("CW\(m.week.week) \(String(m.week.year)) · \(WeekFormatter(cal: store.calendar).shortRange(m.week))")
                                    .font(.caption).foregroundStyle(.secondary)
                            }
                            Spacer()
                            if let st { Text(st.isPast ? "Done" : "\(st.weeksAway)w · \(st.workdaysLeft)d").font(.caption).foregroundStyle(.secondary).monospacedDigit() }
                        }
                    }
                    .onDelete { store.milestones.remove(atOffsets: $0) }
                    Button { addingMilestone = true } label: { Label("Add Milestone", systemImage: "plus") }
                }

                Section("Sprints") {
                    Toggle("Show sprints", isOn: $store.sprint.enabled)
                    if store.sprint.enabled {
                        TextField("Name", text: $store.sprint.name)
                        Stepper("Length: \(store.sprint.lengthWeeks) week\(store.sprint.lengthWeeks == 1 ? "" : "s")", value: $store.sprint.lengthWeeks, in: 1...6)
                        DatePicker("A sprint started on", selection: anchorBinding, displayedComponents: .date)
                        Stepper("That sprint's number: \(store.sprint.firstNumber)", value: $store.sprint.firstNumber, in: 0...9999)
                        Stepper(store.sprint.sprintsPerPI == 0 ? "Program Increments: off" : "\(store.sprint.sprintsPerPI) sprints per PI",
                                value: $store.sprint.sprintsPerPI, in: 0...12)
                        if store.sprint.sprintsPerPI > 0 {
                            Stepper("That sprint is in PI \(store.sprint.firstPI)", value: $store.sprint.firstPI, in: 0...999)
                        }
                        if let i = Sprints.info(for: Date(), config: store.sprint, cal: store.calendar, holidays: store.holidays) {
                            Text("Now: \(store.sprint.name) \(i.number), CW\(i.firstWeek.week)–CW\(i.lastWeek.week), day \(max(i.workday, 1)) of \(i.workdaysTotal)")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                    }
                }

                Section("Quarters") {
                    Toggle("Show quarter", isOn: $store.quarter.enabled)
                    if store.quarter.enabled {
                        Picker("Quarters", selection: $store.quarter.mode) {
                            ForEach(QuarterMode.allCases, id: \.self) { Text($0.title).tag($0) }
                        }
                        if store.quarter.mode == .months {
                            Picker("Fiscal year starts", selection: $store.quarter.fiscalStartMonth) {
                                ForEach(1...12, id: \.self) { Text(months[$0 - 1]).tag($0) }
                            }
                        }
                    }
                }

                Section {
                    NavigationLink {
                        HolidaysView()
                    } label: {
                        LabeledContent("Holidays", value: store.regions.isEmpty && store.customHolidays.isEmpty
                                       ? "None" : store.regions.map(\.flag).sorted().joined(separator: " ") + (store.customHolidays.isEmpty ? "" : " +\(Set(store.customHolidays.map(\.source)).count)"))
                    }
                }

                Section {
                    Text("Add widgets: touch and hold the Home Screen or Lock Screen, tap Edit, then add Weekmark.")
                        .font(.footnote).foregroundStyle(.secondary)
                    Link("Weekmark for Mac", destination: URL(string: "https://rockykusuma.github.io/weekmark/")!)
                    LabeledContent("Version", value: Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "–")
                }
            }
            .navigationTitle("Settings")
            .sheet(isPresented: $addingMilestone) { MilestoneEditor(prefill: nil) }
        }
    }

    private var anchorBinding: Binding<Date> {
        Binding(get: { store.sprint.anchor.date(in: store.calendar) ?? Date() },
                set: { store.sprint.anchor = Day($0, calendar: store.calendar) })
    }
}

struct MilestoneEditor: View {
    @EnvironmentObject private var store: PlanStore
    @Environment(\.dismiss) private var dismiss
    let prefill: WeekRef?
    @State private var name = ""
    @State private var weekText = ""
    @State private var color: MilestoneColor = .orange

    var body: some View {
        let ctx = store.context()
        let parsed = QueryEngine.parseWeek(weekText, ctx)
        NavigationStack {
            Form {
                TextField("Name (e.g. Release 5.0)", text: $name)
                Section {
                    TextField("Week (e.g. 46 or CW3 2027)", text: $weekText)
                        .keyboardType(.numbersAndPunctuation)
                        .autocorrectionDisabled()
                } footer: {
                    switch parsed {
                    case .week(let ref): Text(WeekFormatter(cal: ctx.cal).longRange(ref))
                    case .invalid(let m): Text(m).foregroundStyle(.red)
                    case .none: Text(" ")
                    }
                }
                Picker("Color", selection: $color) {
                    ForEach(MilestoneColor.allCases, id: \.self) { c in
                        Label(c.rawValue.capitalized, systemImage: "flag.fill").tint(c.color).tag(c)
                    }
                }
            }
            .navigationTitle("New Milestone")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Add") {
                        guard case .week(let ref) = parsed else { return }
                        store.milestones.append(Milestone(name: name.trimmingCharacters(in: .whitespaces), week: ref, color: color))
                        store.milestones.sort { $0.week < $1.week }
                        dismiss()
                    }
                    .disabled({ if case .week = parsed { return name.trimmingCharacters(in: .whitespaces).isEmpty }; return true }())
                }
            }
            .onAppear { if let prefill { weekText = "CW\(prefill.week) \(prefill.year)" } }
        }
    }
}

struct HolidaysView: View {
    @EnvironmentObject private var store: PlanStore
    @State private var importing = false
    @State private var newName = ""
    @State private var newDate = Date()

    var body: some View {
        Form {
            Section("Public holidays") {
                ForEach(HolidayRegion.allCases, id: \.self) { r in
                    Toggle("\(r.flag)  \(r.title)", isOn: Binding(
                        get: { store.regions.contains(r) },
                        set: { on in if on { store.regions.insert(r) } else { store.regions.remove(r) } }))
                }
            }
            Section {
                Button { importing = true } label: { Label("Import .ics File", systemImage: "square.and.arrow.down") }
                HStack {
                    TextField("Name", text: $newName)
                    DatePicker("", selection: $newDate, displayedComponents: .date).labelsHidden()
                    Button("Add") {
                        store.customHolidays.append(Holiday(day: Day(newDate, calendar: store.calendar),
                                                            name: newName.isEmpty ? "Holiday" : newName, source: "custom"))
                        newName = ""
                    }
                }
                ForEach(sources, id: \.0) { source, count in
                    LabeledContent(source == "custom" ? "Added by you" : source, value: "\(count) day\(count == 1 ? "" : "s")")
                        .swipeActions { Button("Remove", role: .destructive) { store.customHolidays.removeAll { $0.source == source } } }
                }
            } header: {
                Text("Company holidays")
            } footer: {
                Text("Festivals like Diwali or Eid can't be calculated. Export your company holiday calendar as .ics and import it here. Swipe to remove.")
            }
            Section("Coming up") {
                let list = store.holidays.upcoming(from: Date(), cal: store.calendar, limit: 6)
                if list.isEmpty { Text("No holidays selected").foregroundStyle(.secondary) }
                ForEach(list, id: \.self) { h in
                    if let d = h.day.date(in: store.calendar) {
                        LabeledContent(h.name, value: "\(WeekFormatter(cal: store.calendar).format(d, "EEEdMMM")) · CW\(store.calendar.weekRef(for: d).week)")
                    }
                }
            }
        }
        .navigationTitle("Holidays")
        .fileImporter(isPresented: $importing, allowedContentTypes: [UTType(filenameExtension: "ics") ?? .data, .calendarEvent],
                      allowsMultipleSelection: true) { result in
            guard case .success(let urls) = result else { return }
            for url in urls {
                let scoped = url.startAccessingSecurityScopedResource()
                defer { if scoped { url.stopAccessingSecurityScopedResource() } }
                guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
                let name = url.deletingPathExtension().lastPathComponent
                store.customHolidays.removeAll { $0.source == name }
                store.customHolidays += ICSParser.parse(text, source: name)
            }
        }
    }

    private var sources: [(String, Int)] {
        Dictionary(grouping: store.customHolidays, by: \.source).map { ($0.key, $0.value.count) }.sorted { $0.0 < $1.0 }
    }
}
