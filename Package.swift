// swift-tools-version:5.10
import PackageDescription

let package = Package(
    name: "Weekmark",
    platforms: [.macOS(.v14)],
    dependencies: [
        .package(url: "https://github.com/sparkle-project/Sparkle", from: "2.6.0"),
    ],
    targets: [
        .target(name: "CWCore", path: "Sources/CWCore"),
        .executableTarget(
            name: "Weekmark",
            dependencies: ["CWCore", .product(name: "Sparkle", package: "Sparkle")],
            path: "Sources/Weekmark"
        ),
        .testTarget(name: "CWCoreTests", dependencies: ["CWCore"], path: "Tests/CWCoreTests"),
    ]
)
