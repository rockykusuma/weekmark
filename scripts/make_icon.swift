import AppKit
// Renders a 1024px app icon PNG: swift make_icon.swift out.png
let out = CommandLine.arguments[1]
let S: CGFloat = 1024
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(S), pixelsHigh: Int(S), bitsPerSample: 8,
                           samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                           bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
let body = NSRect(x: 100, y: 100, width: 824, height: 824)
let clip = NSBezierPath(roundedRect: body, xRadius: 185, yRadius: 185)
NSGraphicsContext.current?.cgContext.setShadow(offset: CGSize(width: 0, height: -10), blur: 30, color: NSColor.black.withAlphaComponent(0.3).cgColor)
NSColor.white.setFill(); clip.fill()
NSGraphicsContext.current?.cgContext.setShadow(offset: .zero, blur: 0, color: nil)
clip.addClip()
let band = NSRect(x: 100, y: 924 - 230, width: 824, height: 230)
NSGradient(starting: NSColor(red: 1.0, green: 0.36, blue: 0.30, alpha: 1), ending: NSColor(red: 0.90, green: 0.18, blue: 0.20, alpha: 1))!.draw(in: band, angle: -90)
func draw(_ s: String, size: CGFloat, weight: NSFont.Weight, color: NSColor, centerY: CGFloat, kern: CGFloat = 0) {
    let font = NSFont.systemFont(ofSize: size, weight: weight)
    let desc = font.fontDescriptor.withDesign(.rounded) ?? font.fontDescriptor
    let f = NSFont(descriptor: desc, size: size) ?? font
    let a: [NSAttributedString.Key: Any] = [.font: f, .foregroundColor: color, .kern: kern]
    let str = NSAttributedString(string: s, attributes: a)
    let sz = str.size()
    str.draw(at: NSPoint(x: (S - sz.width) / 2, y: centerY - sz.height / 2))
}
draw("WEEK", size: 120, weight: .heavy, color: .white, centerY: 924 - 115, kern: 14)
draw("CW", size: 400, weight: .bold, color: NSColor(white: 0.13, alpha: 1), centerY: 400)
NSGraphicsContext.restoreGraphicsState()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: out))
