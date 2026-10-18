import SwiftUI

struct FindTab: View {
    @EnvironmentObject private var store: PlanStore
    @State private var query = ""
    @State private var toast: String?
    @State private var detail: WeekRef?

    private let examples = ["46", "Dec 25", "+6w", "46-50", "until CW52", "3 2027"]

    var body: some View {
        NavigationStack {
            let results = QueryEngine.evaluate(query, store.context())
            List {
                if query.isEmpty {
                    Section {
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack {
                                ForEach(examples, id: \.self) { e in
                                    Button(e) { query = e }.buttonStyle(.bordered).buttonBorderShape(.capsule)
                                }
                            }
                        }
                        .listRowInsets(EdgeInsets(top: 8, leading: 16, bottom: 8, trailing: 16))
                    }
                }
                ForEach(results) { r in
                    ResultRow(result: r)
                        .contentShape(Rectangle())
                        .onTapGesture { if let w = r.week, r.kind != .hint { detail = w } }
                        .swipeActions(edge: .trailing) {
                            if let t = r.copyText { Button { copy(t) } label: { Label("Copy", systemImage: "doc.on.doc") }.tint(.accentColor) }
                        }
                        .contextMenu {
                            if let t = r.copyText {
                                Button { copy(t) } label: { Label("Copy “\(t)”", systemImage: "doc.on.doc") }
                                ShareLink(item: t)
                            }
                        }
                }
            }
            .searchable(text: $query, placement: .navigationBarDrawer(displayMode: .always),
                        prompt: "46 · Dec 12 · +6w · 46-50 · until CW52")
            .autocorrectionDisabled()
            .textInputAutocapitalization(.never)
            .navigationTitle("Find a Week")
            .overlay(alignment: .bottom) {
                if let toast {
                    Label(toast, systemImage: "checkmark.circle.fill")
                        .font(.subheadline.weight(.semibold))
                        .padding(.horizontal, 16).padding(.vertical, 10)
                        .background(.thinMaterial, in: Capsule())
                        .padding(.bottom, 12)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                }
            }
            .sheet(item: $detail) { WeekDetailSheet(ref: $0).presentationDetents([.medium, .large]) }
        }
    }

    private func copy(_ text: String) {
        UIPasteboard.general.string = text
        UINotificationFeedbackGenerator().notificationOccurred(.success)
        withAnimation { toast = "Copied \(text)" }
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.6) { withAnimation { toast = nil } }
    }
}

struct ResultRow: View {
    let result: QueryResult

    private var icon: (String, Color) {
        switch result.kind {
        case .week: return ("calendar", .accentColor)
        case .range: return ("arrow.left.and.right", .purple)
        case .date: return ("calendar.day.timeline.left", .blue)
        case .relative: return ("forward.end", .teal)
        case .countdown: return ("hourglass", .orange)
        case .milestone: return ("flag.fill", .orange)
        case .holiday: return ("sun.max.fill", .red)
        case .hint: return ("lightbulb", .secondary)
        case .error: return ("exclamationmark.triangle", .red)
        }
    }

    var body: some View {
        let (sym, color) = icon
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: sym)
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(color)
                .frame(width: 32, height: 32)
                .background(RoundedRectangle(cornerRadius: 8, style: .continuous).fill(color.opacity(0.14)))
            VStack(alignment: .leading, spacing: 2) {
                Text(result.title).font(.subheadline.weight(.semibold))
                Text(result.subtitle).font(.footnote).foregroundStyle(.secondary)
                if let d = result.detail { Text(d).font(.caption).foregroundStyle(.tertiary) }
            }
        }
        .padding(.vertical, 2)
    }
}
