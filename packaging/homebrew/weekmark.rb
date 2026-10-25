# Source of truth: rockykusuma/homebrew-weekmark (Casks/weekmark.rb). Keep in sync on each release.
# Install: brew install --cask rockykusuma/weekmark/weekmark

cask "weekmark" do
  version "1.2"
  sha256 "67fcf840799221313418142b97368843b0070019186f231dc774629a8b78bfe9"

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
