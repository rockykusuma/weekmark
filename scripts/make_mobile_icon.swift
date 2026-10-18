import AppKit
// swift make_mobile_icon.swift out.png  → 1024px square icon, drawn opaque; flatten alpha with sips afterwards
let out = CommandLine.arguments[1]
let S: CGFloat = 1024
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(S), pixelsHigh: Int(S), bitsPerSample: 8,
                           samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                           bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
NSColor.white.setFill(); NSRect(x: 0, y: 0, width: S, height: S).fill()
let band = NSRect(x: 0, y: S - 300, width: S, height: 300)
NSGradient(starting: NSColor(red: 1.0, green: 0.36, blue: 0.30, alpha: 1), ending: NSColor(red: 0.90, green: 0.18, blue: 0.20, alpha: 1))!.draw(in: band, angle: -90)
func draw(_ s: String, size: CGFloat, weight: NSFont.Weight, color: NSColor, centerY: CGFloat, kern: CGFloat = 0) {
    let font = NSFont.systemFont(ofSize: size, weight: weight)
    let f = NSFont(descriptor: font.fontDescriptor.withDesign(.rounded) ?? font.fontDescriptor, size: size) ?? font
    let str = NSAttributedString(string: s, attributes: [.font: f, .foregroundColor: color, .kern: kern])
    let sz = str.size()
    str.draw(at: NSPoint(x: (S - sz.width) / 2, y: centerY - sz.height / 2))
}
draw("WEEK", size: 150, weight: .heavy, color: .white, centerY: S - 150, kern: 18)
draw("CW", size: 470, weight: .bold, color: NSColor(white: 0.13, alpha: 1), centerY: 390)
NSGraphicsContext.restoreGraphicsState()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: out))
