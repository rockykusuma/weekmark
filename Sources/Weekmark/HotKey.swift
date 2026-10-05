import Carbon.HIToolbox

/// System-wide hotkey via Carbon (no Accessibility permission needed).
final class HotKey {
    static var action: (() -> Void)?
    private var ref: EventHotKeyRef?
    private static var handlerInstalled = false

    func register(keyCode: UInt32, modifiers: UInt32) -> Bool {
        unregister()
        if !HotKey.handlerInstalled {
            var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
            InstallEventHandler(GetApplicationEventTarget(), { _, _, _ in
                DispatchQueue.main.async { HotKey.action?() }
                return noErr
            }, 1, &spec, nil, nil)
            HotKey.handlerInstalled = true
        }
        let id = EventHotKeyID(signature: OSType(0x4357_4B59), id: 1) // 'CWKY'
        let status = RegisterEventHotKey(keyCode, modifiers, id, GetApplicationEventTarget(), 0, &ref)
        return status == noErr
    }

    func unregister() {
        if let ref { UnregisterEventHotKey(ref) }
        ref = nil
    }
}
