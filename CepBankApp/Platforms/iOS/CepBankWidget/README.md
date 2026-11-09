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

The summary contains reminder date ranges, categories, names, and start times. The large widget displays the current month's calendar without a separate month heading. Below it, up to four reminders from today through the following six days appear in a left-hand list with a count of any remaining reminders; four icon shortcuts sit in a 2×2 grid on the right: Harcamalar, Hatırlatıcı, Gidilecek yerler, and Alınacaklar. The small widget shows upcoming reminders; the medium widget shows the shortcuts. WidgetKit controls actual refresh timing; the timeline requests a refresh after 30 minutes, but this is not a guaranteed interval.

The widget uses translucent system materials and a clear, removable WidgetKit container background. It includes no wallpaper image; iOS decides whether the Home Screen wallpaper shows through in the user's current widget appearance.

## Build and verify

```sh
dotnet restore CepBankApp/CepBankApp.csproj -p:TargetFramework=net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 --ignore-failed-sources
dotnet build CepBankApp/CepBankApp.csproj -f net10.0-ios -r iossimulator-arm64 -p:EnableCodeSigning=false --no-restore
```

For the registered iPhone, restore and build with `-p:RuntimeIdentifier=ios-arm64` and `-r ios-arm64`. The default iOS build includes the widgets. After installation, open EC Apps → CepBank once to write the summary. The Home Screen widget gallery should have **CepBank Yaklaşan Etkinlikler** (small) and **CepBank Hızlı Erişim** (medium and large). Select the large size for the requested approximately 4×6 layout. iOS controls widget dimensions, so an exact grid size cannot be requested. Verify all four shortcuts and the reminder calendar on the iPhone. A successful build alone does not confirm that the widgets appeared in the gallery or that taps navigated correctly.
