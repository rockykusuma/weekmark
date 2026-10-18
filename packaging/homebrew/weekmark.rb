# Source of truth: rockykusuma/homebrew-weekmark (Casks/weekmark.rb). Keep in sync on each release.
# Install: brew install --cask rockykusuma/weekmark/weekmark

cask "weekmark" do
  version "1.1"
  sha256 "e7a35cb31d15f58a529177952d2a3b4814ac430131827939519607008c73a2b3"

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
