# Source of truth: rockykusuma/homebrew-weekmark (Casks/weekmark.rb). Keep in sync on each release.
# Install: brew install --cask rockykusuma/weekmark/weekmark

cask "weekmark" do
  version "1.2.1"
  sha256 "99527f68045a13c6775a4aa0a05a9757c791fc5169c2f40e88cf27e64e31bb3a"

  url "https://github.com/rockykusuma/weekmark/releases/download/v#{version}/Weekmark-#{version}.dmg"
  name "Weekmark"
  desc "Desktop widget for the current calendar week, with a CW lookup"
  homepage "https://rockykusuma.github.io/weekmark/"

  livecheck do
    url :url
    strategy :github_latest
  end

  auto_updates true
  depends_on macos: :sonoma

  app "Weekmark.app"

  uninstall quit: "com.rockykusuma.weekmark"

  zap trash: [
    "~/Library/Caches/com.rockykusuma.weekmark",
    "~/Library/HTTPStorages/com.rockykusuma.weekmark",
    "~/Library/Preferences/com.rockykusuma.weekmark.plist",
  ]
end
