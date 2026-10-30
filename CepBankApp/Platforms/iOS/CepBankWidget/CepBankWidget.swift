import SwiftUI
import WidgetKit

private let appGroup = "group.com.emir.cepbankapp.cepbankapp"

private struct FinanceSnapshot: Decodable {
    let month: String
    let monthKey: String
    let income: Double
    let expense: Double
    let cardDebt: Double
    let net: Double
    let updatedAt: String

    static func load() -> FinanceSnapshot? {
        guard let directory = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup),
              let data = try? Data(contentsOf: directory.appendingPathComponent("widget-summary.json")) else { return nil }
        guard let snapshot = try? JSONDecoder().decode(FinanceSnapshot.self, from: data) else { return nil }
        let currentMonth = DateFormatter()
        currentMonth.calendar = Calendar(identifier: .gregorian)
        currentMonth.locale = Locale(identifier: "en_US_POSIX")
        currentMonth.dateFormat = "yyyy-MM"
        return snapshot.monthKey == currentMonth.string(from: .now) ? snapshot : nil
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
                    Text(entry.snapshot?.month ?? "AYLIK DURUM").font(.system(size: 10, weight: .medium))
                }.foregroundStyle(.white.opacity(0.78))
                Spacer(minLength: 9)
                if let snapshot = entry.snapshot {
                    Text("Aylık durum").font(.system(size: 12)).foregroundStyle(.white.opacity(0.72))
                    Text(money(snapshot.net))
                        .font(.system(size: family == .systemSmall ? 23 : 30, weight: .bold, design: .rounded))
                        .minimumScaleFactor(0.65).lineLimit(1)
                        .foregroundStyle(snapshot.net < 0 ? Color(red: 1, green: 0.55, blue: 0.55) : .white)
                    Spacer(minLength: 10)
                    if family == .systemSmall {
                        HStack(spacing: 8) {
                            summary("Gelir", money(snapshot.income), .green)
                            summary("Gider", money(snapshot.expense), Color(red: 1, green: 0.55, blue: 0.55))
                        }
                    } else {
                        HStack(spacing: 16) {
                            summary("Gelir", money(snapshot.income), .green)
                            summary("Gider", money(snapshot.expense), Color(red: 1, green: 0.55, blue: 0.55))
                            summary("Kart borcu", money(snapshot.cardDebt), .white)
                        }
                    }
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
        .configurationDisplayName("CepBank Aylık Özet")
        .description("Bu ayın gelir, gider ve kart borcunu gösterir.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}
