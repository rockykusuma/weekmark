import Foundation

/// Minimal key-value store interface so the sync logic can be tested without iCloud.
public protocol SyncKVStore: AnyObject {
    func object(forKey key: String) -> Any?
    func set(_ value: Any?, forKey key: String)
    @discardableResult func synchronize() -> Bool
}

/// iCloud's own `object(forKey:)`, `set(_:forKey:)` (nil removes) and `synchronize()` satisfy the protocol.
extension NSUbiquitousKeyValueStore: SyncKVStore {}

/// Syncs planning settings (milestones, sprints, quarters, holidays, week numbering) between devices through
/// iCloud key-value storage. Values stay in the local UserDefaults in the same format every app already uses;
/// the remote copy wraps each value with a timestamp and the newest write wins, per key.
public final class SettingsSync {
    /// Keys shared across Mac, iPhone and Watch. Widget look-and-feel stays per device.
    public static let syncedKeys = ["numbering", "regions", "customHolidays", "sprint", "quarter", "milestones"]

    private let local: UserDefaults
    private let remote: SyncKVStore
    private let keys: [String]
    private let now: () -> Date
    private var observer: NSObjectProtocol?

    /// Called on the main queue with the keys whose local values were replaced by newer remote values.
    public var onRemoteChange: (([String]) -> Void)?

    public init(local: UserDefaults, remote: SyncKVStore, keys: [String] = SettingsSync.syncedKeys,
                now: @escaping () -> Date = Date.init) {
        self.local = local; self.remote = remote; self.keys = keys; self.now = now
    }

    deinit { if let observer { NotificationCenter.default.removeObserver(observer) } }

    private static func remoteKey(_ key: String) -> String { "wm.\(key)" }
    private static func stampKey(_ key: String) -> String { "sync.stamp.\(key)" }

    func localStamp(_ key: String) -> Double { local.double(forKey: Self.stampKey(key)) }

    func remoteEnvelope(_ key: String) -> (stamp: Double, value: Any?)? {
        guard let env = remote.object(forKey: Self.remoteKey(key)) as? [String: Any],
              let t = env["t"] as? Double else { return nil }
        return (t, env["v"])
    }

    /// Start: observe iCloud changes, pull newer remote values, push local-only ones.
    public func start(observeNotifications: Bool = true) {
        if observeNotifications, let kv = remote as? NSUbiquitousKeyValueStore, observer == nil {
            observer = NotificationCenter.default.addObserver(
                forName: NSUbiquitousKeyValueStore.didChangeExternallyNotification, object: kv, queue: .main
            ) { [weak self] _ in self?.syncNow() }
        }
        syncNow()
    }

    /// Pull newer remote values, then push anything newer (or missing) remotely.
    @discardableResult
    public func syncNow() -> [String] {
        remote.synchronize()
        let changed = pull()
        pushPending()
        if !changed.isEmpty {
            if Thread.isMainThread { onRemoteChange?(changed) }
            else { DispatchQueue.main.async { [onRemoteChange] in onRemoteChange?(changed) } }
        }
        return changed
    }

    /// Record a local edit and push it.
    public func localDidChange(_ key: String) {
        guard keys.contains(key) else { return }
        let t = max(now().timeIntervalSince1970, localStamp(key) + 0.001)
        local.set(t, forKey: Self.stampKey(key))
        push(key, stamp: t)
        remote.synchronize()
    }

    /// Replace local values with remote ones that are strictly newer. Returns the changed keys.
    public func pull() -> [String] {
        var changed: [String] = []
        for key in keys {
            guard let env = remoteEnvelope(key), env.stamp > localStamp(key) else { continue }
            if let v = env.value { local.set(v, forKey: key) } else { local.removeObject(forKey: key) }
            local.set(env.stamp, forKey: Self.stampKey(key))
            changed.append(key)
        }
        return changed
    }

    /// Push keys that are newer locally, or that exist locally but not yet in iCloud
    /// (e.g. settings from before sync existed).
    public func pushPending() {
        for key in keys {
            let lt = localStamp(key)
            let env = remoteEnvelope(key)
            if let env {
                if lt > env.stamp { push(key, stamp: lt) }
            } else if local.object(forKey: key) != nil {
                let t = lt > 0 ? lt : 1   // pre-sync values lose against any real edit elsewhere
                local.set(t, forKey: Self.stampKey(key))
                push(key, stamp: t)
            }
        }
    }

    private func push(_ key: String, stamp: Double) {
        var env: [String: Any] = ["t": stamp]
        if let v = local.object(forKey: key) { env["v"] = v }
        remote.set(env, forKey: Self.remoteKey(key))
    }
}

/// In-memory store for tests and for running without iCloud.
public final class MemoryKVStore: SyncKVStore {
    public var values: [String: Any] = [:]
    public init() {}
    public func object(forKey key: String) -> Any? { values[key] }
    public func set(_ value: Any?, forKey key: String) { values[key] = value }
    public func synchronize() -> Bool { true }
}
