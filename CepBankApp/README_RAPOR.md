# CepBankApp — Kişisel Gelir/Gider Takip Uygulaması Raporu

## 1. Amaç

Cep telefonu üzerinde çalışan, kişisel gelir ve giderleri takip eden, kredi kartı ödeme planlarını ve tekrarlayan ödemeleri yöneten basit bir .NET MAUI uygulaması geliştirmek.

Uygulama:

- Maaş ve diğer gelirleri seçilen günlerde otomatik olarak yazar.
- Kredi taksitlerini seçilen günlerde otomatik olarak gider kaydeder.
- Abonelikleri aylık olarak otomatik gider kaydeder.
- Kredi kartlarının hesap kesim ve son ödeme tarihlerini takip eder.
- Her ay kart bazında ödenecek tutarları ve yaklaşan ödemeleri gösterir.
- Verileri telefonun yerel hafızasında saklar.
- Yalnızca kullanıcının kendi cihazına yüklenmek üzere geliştirilir.

## 2. Mevcut Proje Durumu

Mevcut proje `.NET MAUI Blazor Hybrid` şablonuna dayanıyor. Proje `net10.0-android`, `net10.0-ios` ve `net10.0-maccatalyst` hedeflerini içerecek şekilde hazırlanmış. `MainPage.xaml` içinde Blazor WebView kullanılıyor ve başlangıç ekranı şu anda örnek `Home.razor` sayfasıdır.

Bu yapı mobil arayüz için uygun bir başlangıçtır; ancak veri katmanı, servisler, modeller ve gerçek sayfalar henüz bulunmamaktadır.

## 3. Önerilen Teknoloji Seçimi

### Ana yapı

- **.NET MAUI 10 Blazor Hybrid**
- **Razor/HTML/CSS** arayüz
- **SQLite** yerel veritabanı
- `sqlite-net-pcl` veya Entity Framework Core SQLite
- Yerel bildirim sistemi
- Yerel dosya yedekleme / içe-dışa aktarma

### Önerilen NuGet paketleri

- `sqlite-net-pcl`
- `Microsoft.Extensions.DependencyInjection` (kütüphane zaten mevcut olabilir)
- MAUI yerel bildirim entegrasyonu için platform destekli bildirim bileşeni

Daha az katman ve hızlı geliştirme için `sqlite-net-pcl`, başlangıç aşaması için yeterlidir. Önerilen tablo başına SQL model sınıfları yazılır ve veritabanı uygulama klasöründe oluşturulur.

## 4. Temel Özellikler

### 4.1 Gelirler ve maaş

- Maaş adı
- Aylık tutar
- Aylık ödeme günü
- Başlangıç tarihi
- Sonlandırma tarihi veya süresiz seçenek
- Aktif/pasif durumu

Kullanıcı maaşın 15'i seçerse uygulama ayın 15'inde geliri otomatik oluşturur. Kullanıcı 31'i seçtiğinde 31 günü olmayan aylar için ayın son günü kullanılmalıdır. Aynı mantık tüm aylık tekrarlayan kayıtlar için geçerli olur.

### 4.2 Kredi ve taksitler

- Kredi adı veya banka adı
- Toplam tutar veya aylık taksit tutarı
- Aylık ödeme günü
- Kalan taksit sayısı
- Başlangıç tarihi

Seçili tarih geldiğinde taksit gider kaydı oluşturulur. Kullanıcı taksidi manuel olarak işaretleyebilir. Kalan taksit sıfıra ulaştığında kayıt otomatik pasife geçer.

### 4.3 Abonelikler

- Abonelik adı
- Aylık tutar
- Aylık çekim günü
- İlk ödeme tarihi
- Son ödeme tarihi veya süresiz seçenek

Netflix, telefon faturası, internet, spor salonu gibi kayıtlar aynı sistemle yönetilir. `Abonelikler` ekranı aylık toplamı ve yaklaşan ödemeleri göstermelidir.

### 4.4 Kredi kartları

- Kart adı veya takma ad
- Kartın son dört hanesi
- Hesap kesim günü
- Son ödeme günü
- Limit (isteğe bağlı)
- Mevcut borç veya dönem borcu (manuel girilebilir)

Kart işlemelere dahil olan giderler otomatik eklenirse borç hesaplanabilir. Ancak başlangıç sürümünde en güvenli ve basit yöntem kullanıcıya ay sonu hesap kesim tutarını girme alanı sağlamaktır.

Her kart için kart kesim tarihi geldiğinde dönem borcu, son ödeme tarihi geldiğinde ödeme hatırlatması ve kalan borç durumu takip edilir.

## 5. Veri Modeli

### Kullanıcıya yönelik temel tablolar

- `IncomeSchedule` — tekrarlayan gelirler
- `ExpenseSchedule` — tekrarlayan giderler ve krediler
- `Subscription` — abonelikler
- `CreditCard` — kart bilgileri
- `CardStatement` — dönem kesim tarihleri ve dönem borçları
- `Transaction` — oluşmuş gerçek gelir/gider kayıtları
- `Category` — harcama kategorileri
- `AppSetting` — ayarlar ve son işleme tarihi

### Önemli alanlar

`Transaction` kaydı için önerilen alanlar:

- ID
- Tarih
- Tür (`Income`, `Expense`)
- Tutar
- Açıklama
- Kategori
- Kaynak türü (`Manual`, `Salary`, `Loan`, `Subscription`, `Card`)
- Kaynak kayıt ID
- Gerçekleşti/önizleme durumu

Tekrarlayan kayıtlar yalnızca tahmine dayalı olarak geleceğe çizilir; telefon veritabanında aynı ay için eşleşen bir kayıt zaten varsa yinelenen kayıt oluşturulmaz.

## 6. Otomatik İşleme Mantığı

Her uygulama açılışında ve gün değişiminde:

1. Aktif tekrarlayan gelir ve gider kayıtları kontrol edilir.
2. Son işlenen tarihten bugüne kadar eksik aylar bulunur.
3. Her ay için geçerli ödeme günü hesaplanır.
4. Aynı kaynak ve aynı ay için daha önce işlem oluşturulmadıysa yeni kayıt eklenir.
5. Kart kesim ve son ödeme bildirimleri hesaplanır.
6. Gelecek 7 güne ait bildirimler oluşturulur.

30/31 gibi geçersiz günlerde ayın son günü kullanılmalıdır. Bir kaydın ilk tarihinden önce işlem oluşturulmamalıdır.

Örnek: Maaş 31 günü, Ocak ayında 31; Şubat ayında 28 veya 29 olarak uygulanır.

## 7. Ekranlar

### Ana ekran

- Güncel bakiye
- Bu ay toplam gelir
- Bu ay toplam gider
- Bu ay net fark
- Yaklaşan ödemeler
- Kalan kredi kartı ödemeleri
- En yakındaki 5 işlem

### Gelirler

- Maaş ekleme/düzenleme/silme
- Ek gelir kayıtları
- Tekrarlayan gelirler listesi

### Giderler

- Manuel gider ekleme
- Krediler
- Abonelikler
- Kategori filtresi

### Kredi kartları

- Kart ekleme/düzenleme/silme
- Kartiçin hesap kesim ve son ödeme tarihleri
- Dönem borç tutarı giriş alanı
- Ödeme durumu
- Yaklaşan ödeme takvimi

### İşlem geçmişi

- Tarih aralığı
- Gelir/gider filtresi
- Kategori filtresi
- Kart filtresi
- İçe aktarma ve dışa aktarma

### Ayarlar

- Varsayılan para birimi
- Bildirim ayarı
- Bildirim saati
- Yerel yedekleme
- Yedek dosyasını içe/dışa aktar
- Verileri sıfırlama

## 8. Bildirimler

Önerilen bildirim zamanları:

- Maaş gelmeden önce bilgilendirme
- Son ödeme tarihinden 3 gün önce
- Son ödeme gününde
- Kredi taksit günü
- Abonelik çekim günü
- Aylık özet

Bildirimlerin çalışabilmesi için uygulama ilk açılışta bildirim iznini istemeli ve arka planda güvenilir çalışma iOS platform kısıtları nedeniyle garanti edilmemelidir. Bu nedenle uygulama açıldığında da bekleyen ödemeler ekranda kontrol edilmelidir.

## 9. Güvenlik ve Gizlilik

- Banka hesap bilgisi veya parola alınmaz.
- Kart numarasının yalnızca son dört hanesi saklanır.
- Banka API veya otomatik ödeme entegrasyonu yapılmaz.
- Veriler tamamen cihaz içinde SQLite ile saklanır.
- Hassas finans verileri telefon şifresi veya Face ID/Touch ID ile korunabilir.
- Biyometrik kilit için ayrı bir iOS entitlement/özel yapılandırma gerekebilir.

Şimdilik minimum sürümde biyometrik kilit zorunlu olmasa da uzun vadede Face ID kilidi eklenmesi önerilir.

## 10. Yedekleme ve Veri Kaybı

App Store dışı kişisel kullanımda bile uygulama güncellemeleri veya cihaz değişikliği veri kaybına neden olabilir. İPhone'a yüklenen uygulama silinirse veriler kaybolabilir; cihaz yedeği iOS tarafından uygulama verisini içerebilir ancak garantili değil.

Bu yüzden sürüm 1.1 veya ilk sürümle birlikte şunlar eklenmelidir:

- `.json` veya `.csv` dışa aktarma
- Telefon paylaşımlarına yedek dosyası gönderme
- JSON yedeğini içe aktarma

## 11. Raporlar ve Dashboard

MVP'de önerilen hesaplamalar:

- Güncel tahmini bakiye
- Aylık gelir ve gider toplamı
- Aylık tasarruf oranı
- Kategori bazında gider dağılımı
- Kredi kartı bazında yaklaşan ödemeler
- Kredi taksitleri ve abonelikler için aylık sabit yük
- 30 günlük nakit akışı

## 12. MVP Geliştirme Sırası

### 1. Aşama — Veri altyapısı

- SQLite bağımlılığı ekleme
- Veri modellerini oluşturma
- Repository/Service katmanı ekleme
- Uygulama başlangıçta veritabanını hazırlama

### 2. Aşama — Temel işlemler

- Gelir/gider ekleme
- Gelir/gider listeleme
- Gelir/gider düzenleme/silme
- Ana bakiye hesaplama

### 3. Aşama — Tekrarlayan ödemeler

- Maaş planı
- Kredi planı
- Abonelik planı
- Ay sonu gün düzeltmesi
- Tekrarlayan kayıtların otomatik oluşması

### 4. Aşama — Kredi kartları

- Kart oluşturma
- Kesim ve son ödeme tarihleri
- Kart dönem borcu
- Kart ödemesi işaretleme
- Yaklaşan ödeme ekranı

### 5. Aşama — Bildirimler ve yedekleme

- Bildirim izinleri
- Bildirim planlama
- CSV/JSON dışa aktarma
- JSON içe aktarma

### 6. Aşama — iPhone kurulumu ve kullanım

- Xcode kurulumu
- Bundle Identifier ayarlanması
- Signing ve cihaz seçimi
- Release/Debug derleme
- Fiziksel iPhone'a yükleme

## 13. iPhone'a Kendi Kendine Kurulum

Uygulama App Store'a gönderilmeyecek. Geliştirme aşamasında kullanım için Xcode, .NET MAUI iOS araçları ve Apple imzalama hesabı gereklidir. İzleme hedefindeki iPhone'un geliştirici ortamına/Xcode'a bağlanmış ve imzalama profiline dahil olması gerekir.

Kurulumun sınırları Apple'ın hesabınıza ve güncel Xcode/iOS sürümüne göre değişebilir. En sağlıklı plan, uygulamayı fiziksel iPhone'a `.NET MAUI` + Xcode üzerinden yüklemek ve uygulama için iOS CI/CD/kullanılabilirlik seçeneklerini ileride ayrı değerlendirmektir.

## 14. Proje İçin Önerilen Klasör Yapısı

```text
CepBankApp/
  Models/
  Data/
  Services/
  ViewModels/
  Components/
    Pages/
    Shared/
  Platforms/
  Resources/
  wwwroot/
```

Önerilen ana bileşenler:

- `Models/`
- `Data/DatabaseService.cs`
- `Services/RecurringTransactionService.cs`
- `Services/NotificationService.cs`
- `Services/BackupService.cs`
- `Components/Pages/Dashboard.razor`
- `Components/Pages/Transactions.razor`
- `Components/Pages/Subscriptions.razor`
- `Components/Pages/CreditCards.razor`
- `Components/Pages/Settings.razor`

## 15. İlk Hedef

İlk sürümün kapsamı şu özelliklerle sınırlandırılmalıdır:

- Maaş ekleme
- Manuel gelir/gider ekleme
- Kredi taksiti ekleme
- Abonelik ekleme
- Kredi kartı kesim ve son ödeme günü ekleme
- Aylık kart borcu girme
- Ana dashboard
- Otomatik tekrar
- Yerel bildirimler
- Yerel yedekleme/içe-dışa aktarma

Bu kapsam küçük kalmakta ve kişisel finans uygulamasının temel ihtiyaçlarını karşılamaktadır.

## 16. Önerilen Sonuç

Bu proje için en uygun mimari **SQLite tabanlı .NET MAUI Blazor Hybrid** uygulamasıdır. İlk sürümde net bir `Transaction` ve tekrarlayan kayıt modeli oluşturulmalı, kredi kartı borcu ayrı dönem kaydı olarak tutulmalıdır. Maaş, kredi ve abonelikler yalnızca kayıt oluşturabilir; banka işlemlerini otomatik çekmeye veya gerçek ödeme yapmak için dış entegrasyon kullanılmaz.
