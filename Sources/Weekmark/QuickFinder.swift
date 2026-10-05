import AppKit
import SwiftUI
import Combine
import CWCore

final class QuickFinderModel: ObservableObject {
    @Published var query = "" { didSet { selection = 0; refresh() } }
    @Published var results: [QueryResult] = []
    @Published var selection = 0
    @Published var copied: String?
    @Published var focusToken = 0
    let context: () -> QueryContext

    init(context: @escaping () -> QueryContext) { self.context = context }

    func refresh() {
        results = QueryEngine.evaluate(query, context())
        selection = min(selection, max(0, results.count - 1))
    }

    var selected: QueryResult? { results.indices.contains(selection) ? results[selection] : nil }

    func move(_ delta: Int) {
        guard !results.isEmpty else { return }
        selection = (selection + delta + results.count) % results.count
    }
}

/// Spotlight-style panel: becomes key without activating the app, so focus returns where it was.
final class FinderPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

final class QuickFinder: NSObject, NSWindowDelegate {
    let model: QuickFinderModel
    var onShowInWidget: ((WeekRef) -> Void)?
    private var panel: FinderPanel!
    private var host: NSHostingView<QuickFinderView>!
    private var monitor: Any?
    private var bag = Set<AnyCancellable>()
    private let width: CGFloat = 640

    init(context: @escaping () -> QueryContext) {
        model = QuickFinderModel(context: context)
        super.init()
        host = NSHostingView(rootView: QuickFinderView(model: model, onCopy: { [weak self] in self?.copySelected() },
                                                       onShow: { [weak self] in self?.showSelectedInWidget() }))
        host.sizingOptions = []
        panel = FinderPanel(contentRect: NSRect(x: 0, y: 0, width: width, height: 300),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.contentView = host
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.level = .modalPanel
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.delegate = self

        model.$results.receive(on: RunLoop.main).sink { [weak self] _ in
            DispatchQueue.main.async { self?.fit() }
        }.store(in: &bag)

        monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] e in
            guard let self, self.panel.isKeyWindow else { return e }
            switch Int(e.keyCode) {
            case 125: self.model.move(1); return nil                 // ↓
            case 126: self.model.move(-1); return nil                // ↑
            case 53: self.close(); return nil                        // esc
            case 36, 76:                                             // return
                if e.modifierFlags.contains(.command) { self.showSelectedInWidget() } else { self.copySelected() }
                return nil
            default:
                if e.modifierFlags.contains(.command), e.charactersIgnoringModifiers == "c", self.model.query.isEmpty {
                    self.copySelected(); return nil
                }
                return e
            }
        }
    }

    var isVisible: Bool { panel.isVisible }

    func toggle() { panel.isVisible ? close() : show() }

    func show(query: String = "") {
        model.copied = nil
        model.query = query
        model.refresh()
        fit()
        let screen = NSScreen.screens.first { NSMouseInRect(NSEvent.mouseLocation, $0.frame, false) } ?? NSScreen.main!
        let vf = screen.visibleFrame
        let h = panel.frame.height
        panel.setFrameOrigin(NSPoint(x: vf.midX - width / 2, y: vf.minY + vf.height * 0.72 - h))
        panel.makeKeyAndOrderFront(nil)
        model.focusToken += 1
    }

    func close() { panel.orderOut(nil) }

    func windowDidResignKey(_ notification: Notification) { close() }

    private func fit() {
        let size = NSHostingController(rootView: QuickFinderView(model: model, onCopy: {}, onShow: {}))
            .sizeThatFits(in: CGSize(width: width, height: 2000))
        var f = panel.frame
        let top = f.maxY
        f.size = NSSize(width: width, height: ceil(size.height))
        f.origin.y = top - f.size.height
        panel.setFrame(f, display: true)
    }

    private func copySelected() {
        guard let text = model.selected?.copyText else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
        model.copied = text
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.7) { [weak self] in self?.close() }
    }

    private func showSelectedInWidget() {
        guard let ref = model.selected?.week else { return }
        close()
        onShowInWidget?(ref)
    }
}

struct QuickFinderView: View {
    @ObservedObject var model: QuickFinderModel
    let onCopy: () -> Void
    let onShow: () -> Void
    @FocusState private var focused: Bool

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 12) {
                Image(systemName: "calendar.badge.clock")
                    .font(.system(size: 20, weight: .medium))
                    .foregroundStyle(Color.accentColor)
                TextField("", text: $model.query, prompt: Text("46 · Dec 12 · +6w · 46-50 · until CW52 · milestone"))
                    .textFieldStyle(.plain)
                    .font(.system(size: 22, weight: .medium, design: .rounded))
                    .focused($focused)
                Text("CW\(model.context().current.week)")
                    .font(.system(size: 13, weight: .bold, design: .rounded))
                    .padding(.horizontal, 8).padding(.vertical, 4)
                    .background(Capsule().fill(Color.accentColor.opacity(0.18)))
                    .foregroundStyle(Color.accentColor)
            }
            .padding(.horizontal, 18).padding(.vertical, 16)

            if !model.results.isEmpty {
                Divider().opacity(0.5)
                VStack(spacing: 2) {
                    ForEach(Array(model.results.enumerated()), id: \.element.id) { i, r in
                        row(r, selected: i == model.selection)
                            .contentShape(Rectangle())
                            .onTapGesture(count: 2) { model.selection = i; onCopy() }
                            .onTapGesture { model.selection = i }
                    }
                }
                .padding(8)
            }

            Divider().opacity(0.5)
            HStack(spacing: 14) {
                if let c = model.copied {
                    Label("Copied “\(c)”", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                } else {
                    hint("⏎", "Copy"); hint("⌘⏎", "Show in widget"); hint("↑↓", "Select"); hint("esc", "Close")
                }
                Spacer()
            }
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(.secondary)
            .padding(.horizontal, 18).padding(.vertical, 9)
        }
        .frame(width: 640)
        .modifier(PanelBackground())
        .onChange(of: model.focusToken) { _, _ in focused = true }
        .onAppear { focused = true }
    }

    private func hint(_ key: String, _ label: String) -> some View {
        HStack(spacing: 4) {
            Text(key).font(.system(size: 10, weight: .semibold, design: .rounded))
                .padding(.horizontal, 5).padding(.vertical, 1)
                .background(RoundedRectangle(cornerRadius: 4).fill(.primary.opacity(0.08)))
            Text(label)
        }
    }

    private func icon(_ k: QueryResult.Kind) -> (String, Color) {
        switch k {
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

    private func row(_ r: QueryResult, selected: Bool) -> some View {
        let (sym, color) = icon(r.kind)
        return HStack(alignment: .top, spacing: 12) {
            Image(systemName: sym)
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(color)
                .frame(width: 30, height: 30)
                .background(RoundedRectangle(cornerRadius: 8, style: .continuous).fill(color.opacity(0.14)))
            VStack(alignment: .leading, spacing: 2) {
                Text(r.title).font(.system(size: 14, weight: .semibold)).lineLimit(1)
                Text(r.subtitle).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(1)
                if let d = r.detail {
                    Text(d).font(.system(size: 11)).foregroundStyle(.tertiary).lineLimit(1)
                }
            }
            Spacer(minLength: 8)
            if selected, r.copyText != nil {
                Text("⏎ Copy")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(.secondary)
                    .padding(.top, 8)
            }
        }
        .padding(.horizontal, 10).padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous)
            .fill(selected ? Color.accentColor.opacity(0.18) : .clear))
    }
}

struct PanelBackground: ViewModifier {
    func body(content: Content) -> some View {
        let shape = RoundedRectangle(cornerRadius: 18, style: .continuous)
        if #available(macOS 26.0, *) {
            content
                .background(shape.fill(Color(nsColor: .windowBackgroundColor).opacity(0.55)))
                .glassEffect(.regular, in: shape)
        } else {
            content
                .background(VisualEffect().clipShape(shape))
                .overlay(shape.strokeBorder(.primary.opacity(0.1)))
        }
    }
}
