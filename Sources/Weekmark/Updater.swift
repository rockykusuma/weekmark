import AppKit
import Sparkle

/// Sparkle auto-updates. Feed + public key live in Info.plist (SUFeedURL / SUPublicEDKey).
final class Updater: NSObject, ObservableObject, SPUStandardUserDriverDelegate {
    static let shared = Updater()
    private var controller: SPUStandardUpdaterController!

    @Published var automaticChecks = false {
        didSet { controller?.updater.automaticallyChecksForUpdates = automaticChecks }
    }

    private override init() {
        super.init()
        controller = SPUStandardUpdaterController(startingUpdater: true, updaterDelegate: nil, userDriverDelegate: self)
        automaticChecks = controller.updater.automaticallyChecksForUpdates
    }

    var lastCheck: Date? { controller.updater.lastUpdateCheckDate }

    func checkForUpdates() {
        NSApp.activate(ignoringOtherApps: true)
        controller.checkForUpdates(nil)
    }

    // Menu-bar app: let Sparkle show gentle reminders instead of stealing focus.
    var supportsGentleScheduledUpdateReminders: Bool { true }
}
