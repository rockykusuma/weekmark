import XCTest
@testable import CWCore

final class SyncTests: XCTestCase {
    func defaults(_ name: String) -> UserDefaults {
        let d = UserDefaults(suiteName: "wm.test.\(name).\(UUID().uuidString)")!
        return d
    }

    func testEditOnOneDeviceArrivesOnAnother() {
        let cloud = MemoryKVStore()
        var clock = 1000.0
        let mac = defaults("mac"), phone = defaults("phone")
        let macSync = SettingsSync(local: mac, remote: cloud, now: { Date(timeIntervalSince1970: clock) })
        let phoneSync = SettingsSync(local: phone, remote: cloud, now: { Date(timeIntervalSince1970: clock) })

        mac.set("[{\"name\":\"Release\"}]", forKey: "milestones")
        macSync.localDidChange("milestones")
        clock += 10
        XCTAssertEqual(phoneSync.syncNow(), ["milestones"])
        XCTAssertEqual(phone.string(forKey: "milestones"), "[{\"name\":\"Release\"}]")
        XCTAssertEqual(phoneSync.syncNow(), [], "second sync is a no-op")
    }

    func testNewestWriteWins() {
        let cloud = MemoryKVStore()
        var clock = 1000.0
        let a = defaults("a"), b = defaults("b")
        let sa = SettingsSync(local: a, remote: cloud, now: { Date(timeIntervalSince1970: clock) })
        let sb = SettingsSync(local: b, remote: cloud, now: { Date(timeIntervalSince1970: clock) })
        a.set("iso", forKey: "numbering"); sa.localDidChange("numbering")
        clock += 5
        b.set("us", forKey: "numbering"); sb.localDidChange("numbering")
        clock += 5
        sa.syncNow()
        XCTAssertEqual(a.string(forKey: "numbering"), "us")
        // An older remote value never overwrites a newer local one.
        clock += 5
        a.set("system", forKey: "numbering"); sa.localDidChange("numbering")
        cloud.values["wm.numbering"] = ["t": 1.0, "v": "iso"]
        XCTAssertEqual(sa.pull(), [])
        XCTAssertEqual(a.string(forKey: "numbering"), "system")
    }

    func testPreSyncValuesAreUploadedButLoseToRealEdits() {
        let cloud = MemoryKVStore()
        let mac = defaults("mac2"), phone = defaults("phone2")
        // The Mac had milestones before sync existed (no stamp).
        mac.set("[mac]", forKey: "milestones")
        let macSync = SettingsSync(local: mac, remote: cloud, now: { Date(timeIntervalSince1970: 5000) })
        macSync.syncNow()
        XCTAssertNotNil(cloud.values["wm.milestones"])
        // The phone also had a pre-sync value: the existing remote copy wins only if newer — both are stamp 1,
        // so the phone keeps its own until someone edits.
        let phoneSync = SettingsSync(local: phone, remote: cloud, now: { Date(timeIntervalSince1970: 6000) })
        XCTAssertEqual(phoneSync.syncNow(), ["milestones"], "phone with no value takes the Mac's")
        XCTAssertEqual(phone.string(forKey: "milestones"), "[mac]")
        // A real edit on the phone wins on the Mac.
        phone.set("[phone]", forKey: "milestones"); phoneSync.localDidChange("milestones")
        XCTAssertEqual(macSync.syncNow(), ["milestones"])
        XCTAssertEqual(mac.string(forKey: "milestones"), "[phone]")
    }

    func testArraysAndDeletionsSync() {
        let cloud = MemoryKVStore()
        var clock = 100.0
        let a = defaults("arr-a"), b = defaults("arr-b")
        let sa = SettingsSync(local: a, remote: cloud, now: { Date(timeIntervalSince1970: clock) })
        let sb = SettingsSync(local: b, remote: cloud, now: { Date(timeIntervalSince1970: clock) })
        a.set(["dk", "in"], forKey: "regions"); sa.localDidChange("regions")
        clock += 1; sb.syncNow()
        XCTAssertEqual(b.stringArray(forKey: "regions"), ["dk", "in"])
        clock += 1; a.removeObject(forKey: "regions"); sa.localDidChange("regions")
        clock += 1; sb.syncNow()
        XCTAssertNil(b.object(forKey: "regions"))
    }

    func testUnsyncedKeysAreIgnored() {
        let cloud = MemoryKVStore()
        let a = defaults("ign")
        let s = SettingsSync(local: a, remote: cloud)
        a.set("xlarge", forKey: "size")
        s.localDidChange("size")
        s.syncNow()
        XCTAssertNil(cloud.values["wm.size"])
    }

    /// Regression: the iCloud store must use its own setter (a wrapper once recursed forever).
    func testUbiquitousStoreConformanceDoesNotRecurse() {
        let kv: SyncKVStore = NSUbiquitousKeyValueStore.default
        kv.set(["t": 1.0, "v": "x"], forKey: "wm.test.recursion")
        kv.set(nil, forKey: "wm.test.recursion")
        XCTAssertNil(kv.object(forKey: "wm.test.recursion"))
    }
}
