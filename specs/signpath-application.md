# SignPath Foundation application: draft answers

For Rakesh to submit at https://signpath.org/ (free code signing for open-source projects). Check the live form; field names may differ.

| Field | Answer |
|---|---|
| Project name | Weekmark |
| Project URL | https://github.com/rockykusuma/weekmark |
| Website | https://rockykusuma.github.io/weekmark/ |
| Licence | MIT (OSI-approved), see `LICENSE` |
| Repository | Public, https://github.com/rockykusuma/weekmark |
| Code signing policy page | https://rockykusuma.github.io/weekmark/code-signing.html |
| Description | Weekmark shows the calendar week number on the desktop and answers "when is CW46?". Free, no tracking. The Windows version is a C# / .NET 10 + Avalonia desktop widget with a tray icon. |
| Build system | GitHub Actions, workflow `.github/workflows/windows.yml`, on GitHub-hosted `windows-latest`, from tags `windows-v*` of the public repo. Build: `dotnet publish -c Release -r win-x64 / win-arm64 --self-contained`, packaged with Velopack (`vpk pack`). |
| Artifacts to sign | `Weekmark.exe` and `Weekmark*.dll` (x64 and ARM64); before the first signed release also Velopack's `Setup.exe` and `Update.exe` (see open items). |
| Project / policy slugs | `weekmark` / `release-signing` |
| Roles | Author, reviewer and approver: Rakesh Kusuma (sole maintainer) |
| Privacy | Weekmark collects and transmits no personal data. |

## After approval

1. Add repository secret `SIGNPATH_API_TOKEN` and variable `SIGNPATH_ORGANIZATION_ID`, then set variable `SIGNPATH_ENABLED=true`.
2. Make sure the project and policy slugs in SignPath match `weekmark` and `release-signing`.
3. Add the attribution text SignPath Foundation requires to the code-signing page and the README.

## Open items

- **Signing scope.** The CI signing step currently signs only `Weekmark.exe` and the `Weekmark*.dll` files in `publish/`.
  Velopack creates `Setup.exe` and `Update.exe` while packing, so they are not covered. Before the first signed release,
  sign them too: either pass `vpk pack --signTemplate` pointing at a script that submits to SignPath, or add a second
  signing pass over the `releases/` output.
- Confirm the policy wording and slugs against what SignPath asks for during onboarding.
