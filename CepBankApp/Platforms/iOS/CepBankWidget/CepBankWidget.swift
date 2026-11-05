import SwiftUI
import WidgetKit

private let appGroup = "group.com.emir.cepbankapp.cepbankapp"

private struct FinanceSnapshot: Decodable {
    let accountName: String?
    let accountBalance: Double?
    let cardDebt: Double
    let updatedAt: String

    static func load() -> FinanceSnapshot? {
        guard let directory = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup),
              let data = try? Data(contentsOf: directory.appendingPathComponent("widget-summary.json")) else { return nil }
        return try? JSONDecoder().decode(FinanceSnapshot.self, from: data)
    }
}

private struct FinanceEntry: TimelineEntry {
    let date: Date
    let snapshot: FinanceSnapshot?
}

private struct FinanceProvider: TimelineProvider {
    func placeholder(in context: Context) -> FinanceEntry { FinanceEntry(date: .now, snapshot: nil) }
    func getSnapshot(in context: Context, completion: @escaping (FinanceEntry) -> Void) {
        completion(FinanceEntry(date: .now, snapshot: FinanceSnapshot.load()))
    }
    func getTimeline(in context: Context, completion: @escaping (Timeline<FinanceEntry>) -> Void) {
        let entry = FinanceEntry(date: .now, snapshot: FinanceSnapshot.load())
        completion(Timeline(entries: [entry], policy: .after(.now.addingTimeInterval(30 * 60))))
    }
}

private struct FinanceWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: FinanceEntry

    private func money(_ value: Double) -> String {
        value.formatted(.currency(code: "TRY").locale(Locale(identifier: "tr_TR")))
    }

    var body: some View {
        ZStack {
            if family == .systemMedium {
                LinearGradient(colors: [Color(red: 0.83, green: 0.82, blue: 0.98), Color(red: 0.79, green: 0.91, blue: 0.96), Color(red: 0.97, green: 0.84, blue: 0.91)], startPoint: .topLeading, endPoint: .bottomTrailing)
                Circle()
                    .fill(.white.opacity(0.55))
                    .frame(width: 170, height: 170)
                    .blur(radius: 45)
                    .offset(x: -120, y: -60)
                Circle()
                    .fill(Color(red: 0.83, green: 0.75, blue: 0.98).opacity(0.33))
                    .frame(width: 150, height: 150)
                    .blur(radius: 42)
                    .offset(x: 135, y: 65)
            } else {
                LinearGradient(colors: [Color(red: 0.12, green: 0.15, blue: 0.42), Color(red: 0.20, green: 0.29, blue: 0.56)], startPoint: .topLeading, endPoint: .bottomTrailing)
            }
            if family == .systemMedium {
                shortcuts
            } else {
                summaryView
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var shortcuts: some View {
        GeometryReader { geometry in
            let sectionHeight = (geometry.size.height - 22) / 2
            VStack(spacing: 6) {
                section("CepBank", height: sectionHeight) {
                    HStack(spacing: 5) {
                        shortcut("Kredi kartı", symbol: "creditcard.fill", path: "credit-cards", height: sectionHeight - 26)
                        shortcut("Yemek kartı", symbol: "fork.knife", path: "meal-card", height: sectionHeight - 26)
                        shortcut("Yakıt kartı", symbol: "fuelpump.fill", path: "fuel-card", height: sectionHeight - 26)
                    }
                }
                section("Notlarım", height: sectionHeight) {
                    HStack(spacing: 5) {
                        shortcut("Alınacaklar", symbol: "cart.fill", path: "shopping/new", height: sectionHeight - 26)
                        shortcut("Hatırlatıcı", symbol: "bell.fill", path: "reminder/new", height: sectionHeight - 26)
                        shortcut("Gidilecek yerler", symbol: "mappin.and.ellipse", path: "place/new", height: sectionHeight - 26)
                    }
                }
            }
            .padding(8)
            .frame(width: geometry.size.width, height: geometry.size.height)
        }
    }

    private func section<Content: View>(_ title: String, height: CGFloat, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(title)
                .font(.system(size: 10, weight: .semibold, design: .rounded))
                .foregroundStyle(Color(red: 0.23, green: 0.25, blue: 0.39))
                .padding(.leading, 3)
            content()
        }
        .padding(.horizontal, 6)
        .padding(.vertical, 5)
        .frame(height: height)
        .frame(maxWidth: .infinity)
        .background(.white.opacity(0.34), in: RoundedRectangle(cornerRadius: 19))
        .overlay(RoundedRectangle(cornerRadius: 19).strokeBorder(.white.opacity(0.68), lineWidth: 1))
    }

    private func shortcut(_ title: String, symbol: String, path: String, height: CGFloat) -> some View {
        Link(destination: URL(string: "ecappscepbank://widget/\(path)")!) {
            HStack(spacing: 3) {
                Image(systemName: symbol)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Color(red: 0.36, green: 0.39, blue: 0.57))
                    .frame(width: 14)
                Text(title)
                    .font(.system(size: 9.5, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color(red: 0.18, green: 0.21, blue: 0.35))
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
                Spacer(minLength: 0)
            }
            .frame(maxWidth: .infinity)
            .frame(height: max(25, height))
            .padding(.horizontal, 5)
            .background(LinearGradient(colors: [.white.opacity(0.72), .white.opacity(0.35)], startPoint: .topLeading, endPoint: .bottomTrailing), in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(.white.opacity(0.8), lineWidth: 1))
        }
    }

    private var summaryView: some View {
            VStack(alignment: .leading, spacing: 0) {
                HStack {
                    Text("CEPBANK").font(.system(size: 11, weight: .bold, design: .rounded)).tracking(1.6)
                    Spacer()
                    Text("HESAP ÖZETİ").font(.system(size: 10, weight: .medium))
                }.foregroundStyle(.white.opacity(0.78))
                Spacer(minLength: 9)
                if let snapshot = entry.snapshot {
                    Text(snapshot.accountName ?? "Banka hesabı seçilmedi")
                        .font(.system(size: 12)).foregroundStyle(.white.opacity(0.72)).lineLimit(1)
                    Text(snapshot.accountBalance.map(money) ?? "—")
                        .font(.system(size: family == .systemSmall ? 23 : 30, weight: .bold, design: .rounded))
                        .minimumScaleFactor(0.65).lineLimit(1).foregroundStyle(.white)
                    Spacer(minLength: 10)
                    summary("Kart borcu", money(snapshot.cardDebt), Color(red: 1, green: 0.70, blue: 0.70))
                } else {
                    Text("Özet hazır değil").font(.system(size: 17, weight: .semibold)).foregroundStyle(.white)
                    Text("CepBank’ı açarak verileri güncelle.").font(.system(size: 11)).foregroundStyle(.white.opacity(0.7))
                    Spacer()
                }
            }.padding(16)
    }

    private func summary(_ title: String, _ value: String, _ color: Color) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(.system(size: 10)).foregroundStyle(.white.opacity(0.7))
            Text(value).font(.system(size: family == .systemSmall ? 11 : 14, weight: .semibold, design: .rounded))
                .foregroundStyle(color).lineLimit(1).minimumScaleFactor(0.65)
        }.frame(maxWidth: .infinity, alignment: .leading)
    }
}

@main
struct CepBankWidgets: WidgetBundle {
    var body: some Widget {
        CepBankSummaryWidget()
        CepBankShortcutsWidget()
    }
}

struct CepBankSummaryWidget: Widget {
    let kind = "CepBankSummary"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: FinanceProvider()) { entry in
            FinanceWidgetView(entry: entry)
        }
        .configurationDisplayName("CepBank Hesap Özeti")
        .description("Seçili banka hesabının bakiyesini ve kart borcunu gösterir.")
        .supportedFamilies([.systemSmall])
    }
}

struct CepBankShortcutsWidget: Widget {
    let kind = "CepBankShortcuts"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: FinanceProvider()) { entry in
            if #available(iOS 17.0, *) {
                FinanceWidgetView(entry: entry)
                    .containerBackground(for: .widget) {
                        LinearGradient(colors: [Color(red: 0.83, green: 0.82, blue: 0.98), Color(red: 0.79, green: 0.91, blue: 0.96), Color(red: 0.97, green: 0.84, blue: 0.91)], startPoint: .topLeading, endPoint: .bottomTrailing)
                    }
            } else {
                FinanceWidgetView(entry: entry)
            }
        }
        .configurationDisplayName("CepBank Hızlı Erişim")
        .description("Kartlar ve notlar için altı hızlı kısayol.")
        .supportedFamilies([.systemMedium])
        .contentMarginsDisabled()
    }
}
