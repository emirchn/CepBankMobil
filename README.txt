CEPBANK — KİŞİSEL FİNANS TAKİP UYGULAMASI
=========================================

CepBank, kişinin banka hesaplarını, kredi kartlarını, aboneliklerini, kredilerini
ve yemek/yakıt kartlarını tek yerden izlemesi için geliştirilen bir mobil finans
uygulamasıdır. Amaç, farklı bankaların uygulamalarına dağılmış bilgileri kişinin
kendi kayıtlarıyla bir araya getirmek; dönem borcunu, yaklaşan ödeme günlerini,
aylık sabit giderleri ve günlük harcamaları daha anlaşılır hale getirmektir.

Bu depo uygulamanın kaynak kodunu içerir. CepBank bir banka, ödeme kuruluşu veya
yatırım hizmeti değildir. Banka hesabına bağlanmaz; gerçek para transferi,
otomatik tahsilat veya bankaya kredi kartı ödemesi yapmaz. Uygulamadaki tutarlar
kullanıcının girdiği kayıtlara dayanır.


HANGİ SORUNA ÇÖZÜM BULUR?
-----------------------

Birden fazla hesabı ve kartı olan birinin hangi kartta ne kadar borcu kaldığını,
hangi harcamanın hangi ekstreye girdiğini, aboneliklerinin ayda toplam kaç TL
tuttuğunu ve kredi taksitlerinin ne zaman ödeneceğini takip etmesi zorlaşabilir.
CepBank bu bilgileri aynı yerde toplar. Harcamaları kart, kategori, gün ve ekstre
dönemi bazında incelemeyi; hesap ve kart hareketlerini geriye dönük görmeyi
kolaylaştırır. Böylece kullanıcı kendi bütçesini ve yaklaşan yükümlülüklerini
daha net görebilir.


KİMLER İÇİN?
------------

- Birden fazla banka hesabı veya kredi kartı kullananlar.
- Kart kesim tarihlerini ve taksitlerin hangi döneme yansıyacağını izlemek
  isteyenler.
- Abonelik, kredi taksiti ve diğer düzenli ödemelerin aylık yükünü görmek
  isteyenler.
- Yemek ve yakıt kartı bakiyelerini ayrı takip edenler.
- Finans kayıtlarını otomatik banka erişimi vermeden, kendi cihazında tutmayı
  tercih edenler.


UYGULAMANIN SUNDUKLARI
----------------------

Genel bakış
  Özet ekranı tüm kayıtlı banka hesaplarının güncel bakiyelerini ve seçilen
  banka logolarını gösterir. Kredi kartlarının güncel borcu, mevcut dönem borcu
  ve varsa kullanılabilir limiti burada görülebilir. Yemek ve yakıt kartlarının
  bakiyesiyle son beş hareketi de özetlenir. USD, EUR ve altın fiyatları için
  ayrıca internetten alınan bir piyasa bilgi şeridi bulunur; bu fiyatlar yalnızca
  bilgilendirme amaçlıdır.

Banka hesapları
  Hesap adı, IBAN ve banka logosuyla ayrı hesaplar oluşturulabilir. Her hesapta
  bakiye ekleme, harcama kaydetme, güncel bakiye ve hareketleri görme imkânı
  vardır. Hesaplar düzenlenebilir ve onay verilerek silinebilir. Uygulama,
  bankadan gerçek bakiyeyi çekmez; görünen bakiye kaydedilen işlemlerle oluşur.

Kredi kartları ve harcamalar
  Kart bilgileri, limit, hesap kesim günü ve son ödeme günü kaydedilebilir.
  Normal veya taksitli alışveriş girilebilir; harcamalar kategori ve açıklamayla
  izlenebilir. Kartın güncel borcu, ekstre borcu, gelecek taksitleri ve varsa
  kullanılabilir limiti görüntülenir. Uygulamaya sonradan başlanmışsa mevcut
  ekstre borcu elle sisteme işlenebilir. Kart ödemeleri bir banka hesabıyla
  ilişkilendirilir ve uygulama içindeki bakiye/borç kayıtlarına yansıtılır.
  Kart harcamalarını sıfırlama veya bağlı işlemleri silme, onay ister; kartın
  temel bilgileri korunur.

Ekstre ve hareket raporları
  Her kartın mevcut dönem hareketleri ve taksitli alışverişleri ayrı görülebilir.
  Ay/yıl seçilerek ilgili ekstre dönemi açılır. Ekstre ayı, kesim aralığındaki
  günlerin çoğunun bulunduğu aya göre belirlenir: örneğin 31 Ekim–30 Kasım
  dönemi Kasım ekstresidir. Rapor; dönem toplamını, kategori dağılımını dairesel
  grafikle, günlük harcamaları ve varsa ödemeleri gösterir. Tüm Kart Hareketleri
  ekranı aktif kartların işlemlerini yeniden eskiye listeler; kart, işlem türü,
  kategori ve tarih aralığı gibi filtreler sunar.

Abonelikler
  Aylık abonelikler tutar, başlangıç/bitiş tarihi ve bağlı kredi kartıyla
  kaydedilir. Kayıtlı abonelikler kart bazında gruplanır; her kartın aylık
  toplamı ve genel aylık toplam gösterilir. Vadesi gelen abonelik işlemleri
  uygulamanın kayıt işleme akışında ilgili karta yansıtılır. Silme işlemi onay
  ister.

Krediler
  Krediler bir banka hesabına bağlanır ve taksit planı oluşturulur. Vadesi
  gelen taksitler kullanıcı "Öde" işlemini onaylayınca uygulama içindeki hesap
  bakiyesinden düşülür. Uygulama gerçek bankacılık ödemesi başlatmaz ve
  taksitleri kullanıcı onayı olmadan hesaptan düşmez.

Yemek ve yakıt kartları
  Bu kartların bakiyeleri ve harcamaları banka hesapları ile kredi kartı
  borcundan ayrı tutulur. Yemek kartına bakiye kullanıcı tarafından elle
  yüklenir; ayın ilk günü otomatik yükleme yapılmaz. Yakıt kartında belirlenen
  günde limit yenileme düzeni vardır. Kartların son hareketleri özet ekranında
  da görülebilir.

Bildirimler ve görünüm
  Kart hesap kesim ve son ödeme günleri, abonelikler ve kredi taksitleri için
  cihaz bildirimleri planlanabilir. Hatırlatmaların kaç gün önce gösterileceği
  ayarlardan seçilebilir. Günlük bakiye bildirimi açıldığında saat 16.00 için
  planlanır. Bildirimlerin görünmesi cihaz iznine ve işletim sisteminin
  koşullarına bağlıdır. Açık, koyu, sistem, glassy ve özel tema seçenekleri
  bulunur. Özel temada arka plan fotoğrafı ve vurgu rengi seçilebilir.


VERİLER VE GİZLİLİK
------------------

Finans kayıtları cihazdaki yerel SQLite veritabanında tutulur. Tam kart numarası
ve CVV, kaydedildiklerinde ayrı güvenli depolama alanında saklanır; JSON yedeğine
eklenmez. Ayarlar ekranından veriler JSON dosyası olarak dışa aktarılıp cihazın
paylaşım menüsüyle saklanabilir. Bu yedek finans kayıtları içerdiği için
paylaşılacağı yer dikkatle seçilmelidir. Şu an uygulamada yedeği geri içe
aktarma akışı yoktur. Tüm verileri sıfırlama işlemi ayrıca onay ister.

Uygulama finans kayıtlarını bir CepBank sunucusuna göndermez. Piyasa şeridindeki
kur ve altın bilgilerini almak için harici bir internet kaynağına istek yapar;
bu bölüm internet olmadan güncellenemez.


TEKNİK YAPI VE PROJE DURUMU
--------------------------

CepBank, .NET 10 MAUI Blazor Hybrid ile geliştirilmiştir. Arayüz Razor, HTML ve
CSS; yerel kayıtlar sqlite-net-pcl; bildirimler yerel bildirim paketiyle
oluşturulur. Proje dosyası CepBankApp/CepBankApp.csproj konumundadır. Kaynak
hedefleri Android, iOS ve Mac Catalyst'tir; Windows hedefi Windows üzerinde
tanımlanır. iOS ana ekran widget'ına ait kaynaklar projede bulunsa da normal
derlemede widget kapalıdır ve etkinleştirilmesi uygun App Groups/provisioning
ayarları gerektirir.

Bu depo kişisel finans takibi için geliştirilen bir uygulama projesidir. Gerçek
cihazdaki izinler, bildirim teslimi, platform paketleme ve mağaza dağıtımı
hedef platformda ayrıca doğrulanmalıdır.
