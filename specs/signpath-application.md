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
| Artifacts to sign | `Weekmark.exe` and `Weekmark*.dll`, Velopack's `Update.exe` and the Velopack installer `Weekmark-win-<arch>-Setup.exe` (x64 and ARM64). |
| Project / policy slugs | `weekmark` / `release-signing` |
| Roles | Author, reviewer and approver: Rakesh Kusuma (sole maintainer) |
| Privacy | Weekmark collects and transmits no personal data. |

## How CI signs (already in `.github/workflows/windows.yml`, switched off)

Signing runs only for `windows-v*` tags and only when the repository variable `SIGNPATH_ENABLED` is `true`; every
other build (pushes, pull requests, manual runs) stays unsigned. Each architecture's `package` job makes two signing
requests through `signpath/github-action-submit-signing-request@v2`:

1. **Before `vpk pack`** (artifact configuration `weekmark-files`): a zip with `app/Weekmark.exe`, `app/Weekmark*.dll`
   (`Weekmark.dll`, `Weekmark.Core.dll`) and `vpk/update_x64.exe` or `vpk/update_arm64.exe`. The signed files replace
   the ones in `publish/` and in vpk's vendor folder; vpk copies `update_<arch>.exe` byte for byte into the packages as
   `Update.exe` (checked for vpk 1.2.161), so the installed and portable `Update.exe` is signed.
2. **After `vpk pack`** (artifact configuration `weekmark-setup`): a zip with `Weekmark-win-<arch>-Setup.exe`. The
   signed file replaces it in `releases/`. Nothing hashes Setup.exe (the release feed only hashes the `.nupkg`), so
   signing it after packing is safe; vpk itself signs Setup.exe at that point when it signs.

The staging step (copying the files into `unsigned/`) runs on every build, so a vpk update that moves its vendor
folder fails CI at once rather than at the first signed release.

## After approval: what Rakesh fills in

1. In SignPath, project slug `weekmark`, signing policy slug `release-signing`, with GitHub.com as the trusted build
   system for `rockykusuma/weekmark` (SignPath's GitHub integration; the action needs it to fetch the artifacts).
2. Two artifact configurations in that project, with exactly these slugs (check the syntax in SignPath's editor):
   - `weekmark-files`
     ```xml
     <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
       <zip-file>
         <pe-file-set>
           <include path="app/Weekmark.exe" />
           <include path="app/Weekmark*.dll" max-matches="unbounded" />
           <include path="vpk/update_*.exe" />
           <for-each><authenticode-sign /></for-each>
         </pe-file-set>
       </zip-file>
     </artifact-configuration>
     ```
   - `weekmark-setup`
     ```xml
     <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
       <zip-file>
         <pe-file path="Weekmark-win-*-Setup.exe"><authenticode-sign /></pe-file>
       </zip-file>
     </artifact-configuration>
     ```
3. GitHub repository settings: secret `SIGNPATH_API_TOKEN` (a SignPath CI user's API token, with submitter rights on
   the project), variable `SIGNPATH_ORGANIZATION_ID`, then variable `SIGNPATH_ENABLED` = `true`.
4. Push the next `windows-v*` tag and approve its four signing requests (two per architecture) in SignPath.
5. Check the result on a Windows PC: `Get-AuthenticodeSignature` on `Setup.exe`, and on `Update.exe`,
   `current\Weekmark.exe` and `current\Weekmark.dll` in the Portable zip, should all say `Valid`.
6. Add the attribution text SignPath Foundation requires to the code-signing page and the README, and change the
   "unsigned" wording there, in `Windows/README.md` and in the release notes.

## Open items

- **Velopack's launcher stub stays unsigned.** The `Weekmark.exe` at the root of the install folder and of the
  Portable zip (the Start menu shortcut's target) is a stub vpk generates while packing (it copies the app's icon and
  version into it), so it can't be signed beforehand and is inside the `.nupkg`, so it can't be signed afterwards
  without breaking the release feed's hashes. Signing it needs `vpk pack --signTemplate "<command> {{file}}"`, a
  per-file command run during packing; that would have to submit each file through SignPath's API or PowerShell module
  (`Submit-SigningRequest`) instead of the GitHub action. Ask SignPath during onboarding whether the Foundation's
  release policy accepts such submissions from this workflow. It runs from the user's own disk, so it doesn't trigger
  SmartScreen; the downloaded Setup.exe is the file that matters there.
- Confirm the policy wording and slugs against what SignPath asks for during onboarding.
