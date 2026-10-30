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
            LinearGradient(colors: [Color(red: 0.12, green: 0.15, blue: 0.42), Color(red: 0.20, green: 0.29, blue: 0.56)], startPoint: .topLeading, endPoint: .bottomTrailing)
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
struct CepBankWidget: Widget {
    let kind = "CepBankSummary"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: FinanceProvider()) { entry in
            FinanceWidgetView(entry: entry)
        }
        .configurationDisplayName("CepBank Hesap Özeti")
        .description("Seçili banka hesabının bakiyesini ve kart borcunu gösterir.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}
