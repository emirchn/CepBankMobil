# CepBank iOS widget

The native WidgetKit extension is kept in the repository but is disabled for normal builds (`EnableCepBankWidget=false`). After App Groups provisioning is available, build with `-p:EnableCepBankWidget=true` to build and embed the extension. The app entitlement file is `App.entitlements` in this folder; it is intentionally outside the auto-detected `Platforms/iOS/Entitlements.plist` location so normal device builds do not request App Groups.

## Apple signing setup

The connected Apple account currently shows **Join the Apple Developer Program**. Its Personal Team provisioning profile does not contain the App Groups entitlement. A live finance-summary widget on an iPhone requires an Apple Developer Program team with App Groups access; a successful MAUI compilation using the Personal Team is not a working device widget.

Register the App Group `group.com.emir.cepbankapp.cepbankapp` in the Apple Developer account. Enable it for both App IDs:

- `com.emir.cepbankapp.cepbankapp`
- `com.emir.cepbankapp.cepbankapp.widget`

Regenerate the provisioning profiles for both IDs after enabling the group. The extension's bundle ID must remain a child of the app's bundle ID. Both targets must be signed by the same team. The app and extension entitlement files in this directory tree contain the matching group ID. `CepBankApp.csproj` uses team `CP6N6FNF43` for the device widget build; update `_WidgetTeamId` if the app moves to another team. The device build requires a signed extension with its own `embedded.mobileprovision` and fails if Xcode cannot obtain one.

## Data flow

The MAUI app writes `widget-summary.json` atomically to the App Group container after relevant changes and when the overview loads. The widget reads only that summary. The private SQLite database and SecureStorage card details stay in the app's own container.

The summary contains the bank account selected for the home screen, its current balance, and the separate total credit-card debt. If no account is selected, the widget shows an account-selection prompt. WidgetKit controls actual refresh timing; the timeline requests a refresh after 30 minutes, but this is not a guaranteed interval.

## Build and verify

```sh
dotnet restore CepBankApp/CepBankApp.csproj -p:TargetFramework=net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 --ignore-failed-sources
dotnet build CepBankApp/CepBankApp.csproj -f net10.0-ios -r iossimulator-arm64 -p:EnableCodeSigning=false --no-restore
```

When provisioning is ready, add `-p:EnableCepBankWidget=true` to restore and build commands. After installation, open CepBank once to write the summary, then add “CepBank Aylık Özet” from the iPhone Home Screen widget gallery. Check the small and medium sizes, a transaction change, a card debt change, and a month rollover. A successful build alone does not confirm that the widget appeared on a device.
