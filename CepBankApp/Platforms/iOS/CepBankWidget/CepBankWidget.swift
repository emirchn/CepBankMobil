import SwiftUI
import WidgetKit

private let appGroup = "group.com.emir.cepbankapp.cepbankapp"

private struct FinanceSnapshot: Decodable {
    let events: [CalendarEvent]?
    let updatedAt: String

    static func load() -> FinanceSnapshot? {
        guard let directory = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup),
              let data = try? Data(contentsOf: directory.appendingPathComponent("widget-summary.json")) else { return nil }
        return try? JSONDecoder().decode(FinanceSnapshot.self, from: data)
    }
}

private struct CalendarEvent: Decodable {
    let startDate: String
    let endDate: String
    let category: Int
    let name: String?
    let startTime: String?
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

    var body: some View {
        Group {
            if family == .systemLarge {
                largeView
            } else if family == .systemMedium {
                shortcuts
            } else {
                compactView
            }
        }.frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var largeView: some View {
        VStack(spacing: 7) {
            Link(destination: URL(string: "ecappscepbank://widget/reminders")!) {
                calendar
            }
            .frame(maxHeight: .infinity)
            GeometryReader { geometry in
                HStack(spacing: 7) {
                    upcomingEvents
                        .frame(width: geometry.size.width * 0.61, height: geometry.size.height)
                    largeShortcuts
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            .frame(height: 139)
        }
        .padding(10)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var largeShortcuts: some View {
        VStack(spacing: 5) {
            HStack(spacing: 5) {
                iconShortcut("Harcamalar", symbol: "list.bullet.rectangle", path: "expenses")
                iconShortcut("Hatırlatıcı", symbol: "bell.fill", path: "reminder/new")
            }
            HStack(spacing: 5) {
                iconShortcut("Gidilecek yerler", symbol: "mappin.and.ellipse", path: "place/new")
                iconShortcut("Alınacaklar", symbol: "cart.fill", path: "shopping/new")
            }
        }
    }

    private func glassPanel(radius: CGFloat, tint: Color) -> some View {
        RoundedRectangle(cornerRadius: radius)
            .fill(.ultraThinMaterial)
            .overlay {
                RoundedRectangle(cornerRadius: radius)
                    .fill(LinearGradient(
                        colors: [.white.opacity(0.25), tint.opacity(0.14), .white.opacity(0.03)],
                        startPoint: .topLeading, endPoint: .bottomTrailing))
            }
            .overlay {
                RoundedRectangle(cornerRadius: radius)
                    .strokeBorder(LinearGradient(
                        colors: [.white.opacity(0.62), .white.opacity(0.16), .white.opacity(0.35)],
                        startPoint: .topLeading, endPoint: .bottomTrailing), lineWidth: 1)
            }
            .shadow(color: .black.opacity(0.17), radius: 8, x: 0, y: 4)
    }

    private func iconShortcut(_ title: String, symbol: String, path: String) -> some View {
        Link(destination: URL(string: "ecappscepbank://widget/\(path)")!) {
            VStack(spacing: 3) {
                Image(systemName: symbol)
                    .font(.system(size: 18, weight: .semibold))
                Text(title)
                    .font(.system(size: 8, weight: .semibold, design: .rounded))
                    .lineLimit(2)
                    .minimumScaleFactor(0.75)
                    .multilineTextAlignment(.center)
            }
            .foregroundStyle(.primary)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background { glassPanel(radius: 17, tint: Color(red: 0.28, green: 0.64, blue: 0.69)) }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var nextWeekEvents: [(day: Date, event: CalendarEvent)] {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: entry.date)
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd"
        return (0..<7).flatMap { offset -> [(day: Date, event: CalendarEvent)] in
            guard let date = calendar.date(byAdding: .day, value: offset, to: today) else { return [] }
            let key = formatter.string(from: date)
            return (entry.snapshot?.events ?? []).filter { $0.startDate <= key && $0.endDate >= key }.map { (date, $0) }
        }
    }

    private var upcomingEvents: some View {
        Link(destination: URL(string: "ecappscepbank://widget/reminders")!) {
            VStack(alignment: .leading, spacing: 3) {
                Text("Yaklaşan Etkinlikler")
                    .font(.system(size: 10, weight: .bold, design: .rounded))
                    .lineLimit(1)
                let events = Array(nextWeekEvents.prefix(4))
                if events.isEmpty {
                    Text("Bu hafta hatırlatıcı yok").font(.system(size: 10)).foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity, alignment: .leading)
                } else {
                    ForEach(events.indices, id: \.self) { index in
                        let item = events[index]
                        HStack(spacing: 5) {
                            Circle().fill(item.event.category == 1 ? .red : item.event.category == 2 ? .green : .blue).frame(width: 5, height: 5)
                            Text(item.event.name ?? "Etkinlik").lineLimit(1)
                            Spacer(minLength: 2)
                            Text(item.day.formatted(.dateTime.day().month(.abbreviated).locale(Locale(identifier: "tr_TR"))))
                        }
                        .font(.system(size: 9, weight: .medium))
                    }
                    if nextWeekEvents.count > events.count {
                        Text("+\(nextWeekEvents.count - events.count) etkinlik daha")
                            .font(.system(size: 9, weight: .medium))
                            .foregroundStyle(.secondary)
                    }
                }
            }
            .foregroundStyle(.primary)
            .padding(8)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .background { glassPanel(radius: 18, tint: Color(red: 0.31, green: 0.67, blue: 0.61)) }
        }
    }

    private var calendar: some View {
        let cal = Calendar(identifier: .gregorian)
        let first = cal.date(from: cal.dateComponents([.year, .month], from: .now)) ?? .now
        let count = cal.range(of: .day, in: .month, for: first)?.count ?? 30
        let leading = (cal.component(.weekday, from: first) + 5) % 7
        return VStack(spacing: 3) {
            HStack(spacing: 0) {
                ForEach(["Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pa"], id: \.self) { day in
                    Text(day).font(.system(size: 9, weight: .bold)).frame(maxWidth: .infinity)
                }
            }
            ForEach(0..<6, id: \.self) { week in
                HStack(spacing: 0) {
                    ForEach(0..<7, id: \.self) { weekday in
                        let number = week * 7 + weekday - leading + 1
                        if number > 0 && number <= count {
                            let date = cal.date(byAdding: .day, value: number - 1, to: first) ?? first
                            calendarDay(number, date: date)
                        } else {
                            Color.clear.frame(maxWidth: .infinity, maxHeight: .infinity)
                        }
                    }
                }
                .frame(maxHeight: .infinity)
            }
        }
        .padding(8)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .foregroundStyle(.primary)
        .background { glassPanel(radius: 18, tint: Color(red: 0.34, green: 0.53, blue: 0.75)) }
    }

    private func calendarDay(_ number: Int, date: Date) -> some View {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd"
        let day = formatter.string(from: date)
        let categories = Set((entry.snapshot?.events ?? []).filter { $0.startDate <= day && $0.endDate >= day }.map(\.category))
        let today = Calendar.current.isDateInToday(date)
        return VStack(spacing: 1) {
            Text("\(number)")
                .font(.system(size: 10, weight: today ? .bold : .medium))
                .frame(width: 21, height: 17)
                .background(today ? Color.teal.opacity(0.35) : .clear, in: Circle())
            HStack(spacing: 2) {
                ForEach([0, 1, 2], id: \.self) { category in
                    if categories.contains(category) {
                        Circle().fill(category == 1 ? .red : category == 2 ? .green : .blue).frame(width: 4, height: 4)
                    }
                }
            }.frame(height: 4)
        }
        .frame(maxWidth: .infinity)
        .frame(maxHeight: .infinity)
    }

    private var shortcuts: some View {
        GeometryReader { geometry in
            let sectionHeight = (geometry.size.height - 22) / 2
            VStack(spacing: 6) {
                section("CepBank", height: sectionHeight) {
                    HStack(spacing: 5) {
                        shortcut("Harcamalar", symbol: "list.bullet.rectangle", path: "expenses", height: sectionHeight - 26)
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
                .foregroundStyle(.primary)
                .padding(.leading, 3)
            content()
        }
        .padding(.horizontal, 6)
        .padding(.vertical, 5)
        .frame(height: height)
        .frame(maxWidth: .infinity)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 19))
        .overlay(RoundedRectangle(cornerRadius: 19).strokeBorder(.white.opacity(0.25), lineWidth: 1))
    }

    private func shortcut(_ title: String, symbol: String, path: String, height: CGFloat) -> some View {
        Link(destination: URL(string: "ecappscepbank://widget/\(path)")!) {
            HStack(spacing: 3) {
                Image(systemName: symbol)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(.primary)
                    .frame(width: 14)
                Text(title)
                    .font(.system(size: 9.5, weight: .semibold, design: .rounded))
                    .foregroundStyle(.primary)
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
                Spacer(minLength: 0)
            }
            .frame(maxWidth: .infinity)
            .frame(height: max(25, height))
            .padding(.horizontal, 5)
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(.white.opacity(0.25), lineWidth: 1))
        }
    }

    private var compactView: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("CepBank").font(.system(size: 15, weight: .bold, design: .rounded))
            Text("Yaklaşan Etkinlikler").font(.system(size: 11, weight: .semibold))
            let events = Array(nextWeekEvents.prefix(3))
            if events.isEmpty {
                Text("Bu hafta hatırlatıcı yok").font(.system(size: 11)).foregroundStyle(.secondary)
            } else {
                ForEach(events.indices, id: \.self) { index in
                    let item = events[index]
                    HStack(spacing: 4) {
                        Circle().fill(item.event.category == 1 ? .red : item.event.category == 2 ? .green : .blue).frame(width: 5, height: 5)
                        Text(item.event.name ?? "Etkinlik").lineLimit(1)
                    }.font(.system(size: 10))
                }
            }
            Spacer(minLength: 0)
        }
        .padding(13)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 19))
        .overlay(RoundedRectangle(cornerRadius: 19).strokeBorder(.white.opacity(0.24)))
        .padding(7)
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
            if #available(iOS 17.0, *) {
                FinanceWidgetView(entry: entry)
                    .containerBackground(for: .widget) { Color.clear }
            } else {
                FinanceWidgetView(entry: entry)
            }
        }
        .configurationDisplayName("CepBank Yaklaşan Etkinlikler")
        .description("Önümüzdeki haftanın hatırlatıcılarını gösterir.")
        .supportedFamilies([.systemSmall])
        .contentMarginsDisabled()
    }
}

struct CepBankShortcutsWidget: Widget {
    let kind = "CepBankShortcuts"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: FinanceProvider()) { entry in
            if #available(iOS 17.0, *) {
                FinanceWidgetView(entry: entry)
                    .containerBackground(for: .widget) { Color.clear }
            } else {
                FinanceWidgetView(entry: entry)
            }
        }
        .configurationDisplayName("CepBank Hızlı Erişim")
        .description("Harcamalar ve notlar için hızlı kısayollar; büyük boyda takvim ve yaklaşan etkinlikler.")
        .supportedFamilies([.systemMedium, .systemLarge])
        .contentMarginsDisabled()
    }
}
