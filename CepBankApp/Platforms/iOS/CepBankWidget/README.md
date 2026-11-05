# CepBank iOS widget

The native WidgetKit extension is included in normal iOS builds (`EnableCepBankWidget=true`). Pass `-p:EnableCepBankWidget=false` to make an iOS build without it. The app entitlement file is `App.entitlements` in this folder; it is intentionally outside the auto-detected `Platforms/iOS/Entitlements.plist` location so non-widget builds do not request App Groups.

## Apple signing setup

The Apple Developer Program team is `CP6N6FNF43`. The current EC Apps and widget development profiles both contain `group.com.emir.cepbankapp.cepbankapp`. Older profiles without that group must not be selected when signing a widget build.

In Certificates, Identifiers & Profiles, register the App Group `group.com.emir.cepbankapp.cepbankapp` under **Identifiers → + → App Groups**. Enable the **App Groups** capability and assign that group to both App IDs:

- `com.emir.cepbankapp.cepbankapp`
- `com.emir.cepbankapp.cepbankapp.widget`

Regenerate the iOS App Development profile for EC Apps and create an iOS App Development profile for the widget, including the registered iPhone and an Apple Development certificate. Download both through Xcode's **Settings → Apple Accounts → Download Manual Profiles**. The extension's bundle ID must remain a child of the app's bundle ID. Both targets must be signed by the same team. The app and extension entitlement files in this directory tree contain the matching group ID. `CepBankApp.csproj` uses team `CP6N6FNF43` for the device widget build; update `_WidgetTeamId` if the app moves to another team. The device build requires a signed extension with its own `embedded.mobileprovision` and fails if Xcode cannot obtain one.

## Data flow

The MAUI app writes `widget-summary.json` atomically to the App Group container after relevant changes and when the overview loads. The widget reads only that summary. The private SQLite database and SecureStorage card details stay in the app's own container.

The summary contains the bank account selected for the home screen, its current balance, and the separate total credit-card debt. If no account is selected, the widget shows an account-selection prompt. WidgetKit controls actual refresh timing; the timeline requests a refresh after 30 minutes, but this is not a guaranteed interval.

## Build and verify

```sh
dotnet restore CepBankApp/CepBankApp.csproj -p:TargetFramework=net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 --ignore-failed-sources
dotnet build CepBankApp/CepBankApp.csproj -f net10.0-ios -r iossimulator-arm64 -p:EnableCodeSigning=false --no-restore
```

For the registered iPhone, restore and build with `-p:RuntimeIdentifier=ios-arm64` and `-r ios-arm64`. The default iOS build includes the widgets. After installation, open EC Apps → CepBank once to write the summary. The Home Screen widget gallery has **CepBank Hesap Özeti** (small) and **CepBank Hızlı Erişim** (medium, 4×2). The medium widget opens the credit-card picker, meal-card tab, fuel-card tab, and the new reminder, shopping-item, and place forms through the `ecappscepbank://widget/` URL scheme. Verify all six links on the iPhone. A successful build alone does not confirm that the widgets appeared in the gallery or that taps navigated correctly.
