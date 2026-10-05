# Homebrew cask — copy to your tap (e.g. rockykusuma/homebrew-tap/Casks/weekmark.rb)
# after uploading the notarized DMG to a GitHub release. Users install with:
#   brew install --cask rockykusuma/tap/weekmark
cask "weekmark" do
  version "1.0"
  sha256 "REPLACE_WITH_SHA256_FROM_RELEASE_SH"

  url "https://github.com/rockykusuma/weekmark/releases/download/v#{version}/Weekmark-#{version}.dmg"
  name "Weekmark"
  desc "Current calendar week on your desktop, plus a CW lookup from any app"
  homepage "https://github.com/rockykusuma/weekmark"

  depends_on macos: ">= :sonoma"

  app "Weekmark.app"

  uninstall quit: "com.rockykusuma.weekmark"

  zap trash: "~/Library/Preferences/com.rockykusuma.weekmark.plist"
end
