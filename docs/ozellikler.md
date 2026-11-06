# Özellikler (ayrıntılı)

[← README](../README.md)

## Çevrimdışı satış

Sunucuya ulaşılamadığında kasa satışa devam eder; bağlantı gelince satışlar otomatik gönderilir.

- **Ürün okutma:** Kasa, mağazanın ürün/fiyat listesini (`GET /api/pos/offline-catalog`) girişte ve 30 dakikada bir
  `%ProgramData%\LCWaikiki\WPOS\Offline\catalog` altına indirir. Sunucu yanıt vermezse barkod buradan okunur
  (stok bilinmez; satış stoğu eksiye düşürebilir — çevrimiçi satıştaki gibi uyarıyla kaydedilir).
- **Satış:** Ödeme gönderilemezse satış `Offline\pending` altına yazılır, fişe 18 haneli çevrimdışı belge no basılır
  (`9` + mağaza + yyMMdd + kasa no + sıra). Kuyruk, ilk denemenin `Idempotency-Key`'ini kullanır: istek aslında
  sunucuya ulaşmışsa gönderimde aynı fiş döner, çift satış olmaz. Sunucu ayrıca `(StoreId, OfflineReference)`
  tekil index'iyle aynı belgeyi ikinci kez kaydetmez.
- **Çevrimdışıyken yapılamayanlar** (sunucu onayı gerekir): kupon, hediye kartı, indirim, değişim/iade.
- **Gönderim:** 20 sn'de bir, en eski satış önce, yalnızca o satışı yapan kasiyerin oturumuyla. Ağ hatası / oturum süresi
  dolması / sunucu meşgulse durur ve sonra tekrar dener (arka planda PIN penceresi açmaz). Sunucu kuralı reddederse
  satış `Offline\failed`'a taşınır; **Ana Menü → Çevrimdışı Satışlar**'da "Tekrar dene" ya da müdür onayıyla "Arşivle".
- **Fiyat:** Sunucu satışın kasada yapıldığı andaki fiyatı esas alır. Kasanın kataloğu eskiyse, uygulanan fiyat satıştan
  önceki 48 saatte geçerli olmuş bir fiyatsa kabul edilir; hiç geçerli olmamış fiyat reddedilir. 7 günden eski satış
  otomatik kaydedilmez.
- **Gün sonu:** Gönderilmemiş ya da reddedilmiş çevrimdışı satış varken kasa kapanışı ve Z raporu yapılamaz.
- **İade:** Müşterinin fişindeki çevrimdışı belge no ile de fiş bulunur, iade/değişim yapılır.
- **Ayarlar** (kasa `appsettings.json` → `Offline`): `Enabled`, `TerminalNo` (aynı mağazada her kasada farklı; 0 = makine
  adından), `CatalogRefreshMinutes`, `SyncIntervalSeconds`.
- **Sınır:** Kasa uygulaması sunucu yokken yeniden açılırsa giriş yapılamaz (PIN sunucuda doğrulanır); çevrimdışı satış,
  açık oturumla çalışırken bağlantı koptuğunda devreye girer.

## Satış performansı ve hedefler

Panel: **Genel → Satış Performansı** (yalnızca Admin); özet kart admin panosunda.

- **Net ciro** = dönemde kesilen fişler − dönemde yapılan iadeler (işlemi yapan mağazaya); mağaza paneli ve
  X/Z raporuyla aynı tanım. Hediye kartı satışı emanettir, ciroya girmez.
- **Dönem:** Bugün / Bu hafta / Bu ay / Geçen ay / özel aralık (en fazla 366 gün, yerel iş günü).
- **Hedef:** mağaza başına aylık net ciro hedefi ("Hedefleri Düzenle", önceki ayı kopyalama). Dönem ayın
  tamamı değilse gün sayısına göre orantılanır; dönemin bir ayında hedef yoksa o mağazada oran gösterilmez.
- **Tempo:** olması gereken ilerleme = geçen gün / dönemdeki gün. Gerçekleşme bunun 5 puan üstündeyse
  "Önde", altındaysa "Geride", arasında "Hedefte"; %100 ve üstü "Hedef tamam".
- **Sıralama:** hedefe ulaşma oranı, sonra net ciro (sütun başlıklarıyla değiştirilebilir); ciro payı,
  ortalama sepet, önceki eşit döneme göre değişim, günlük net ciro grafiği, en çok satan 10 ürün.
- API: `GET /api/sales-performance?from=yyyy-MM-dd&to=yyyy-MM-dd`, `GET/PUT /api/sales-targets`.
  Hedef değişiklikleri işlem kaydına (eski → yeni) yazılır.

## Fiş yazıcısı (termal)

Her belge (satış, iade/değişim, hediye kartı, kopya) önce PDF olarak kaydedilir
(`%ProgramData%\LCWaikiki\WPOS\Receipts`), yazıcı açıksa ardından basılır. Yazdırma hatası satışı bozmaz:
loglanır, Fiş ekranında kırmızı "Yazıcı: ..." uyarısı çıkar; belge "Son Belge Tekrarı" ile yeniden basılabilir.

Kasa `appsettings.json` → `Printer`:

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `Enabled` | `false` | Açın |
| `Name` | — | Windows'taki yazıcı adı (Ayarlar → Yazıcılar). `file:C:\yol\fis.bin` ham çıktıyı dosyaya yazar (tanılama) |
| `Mode` | `EscPos` | `EscPos`: fiş 203 dpi siyah-beyaz resim olarak ham ESC/POS (GS v 0) gönderilir, sonra kesilir — hızlı, Türkçe karakter sorunu yok (Epson TM, Xprinter, Bixolon, Sewoo ve uyumlular). `Driver`: Windows sürücüsüyle kağıt genişliğine ölçeklenerek basılır (ESC/POS desteklemeyen yazıcılar) |
| `PaperWidthMm` | `80` | `80` (576 nokta) ya da `58` (384 nokta); `DotsPerLine` ile elle de verilebilir |
| `AutoCut` / `FeedLinesBeforeCut` | `true` / `4` | Kesiciden önce kağıt besleme |
| `Threshold` | `180` | Siyah/beyaz eşiği; soluk baskıda artırın |
| `OpenDrawerOnCashSale` | `true` | Nakit ödemede çekmece açılır (`CashDrawer:PrinterName` boşsa fiş yazıcısı kullanılır) |

Kurulum: yazıcıyı üreticinin sürücüsüyle ya da "Generic / Text Only" ile Windows'a kurun, adını `Printer:Name`'e yazın.

## Kategoriler

Panel: **Katalog → Kategoriler** (yalnızca Admin). Ekleme (2 harfli kod addan önerilir), ad/açıklama değiştirme,
silme. Kategori kodu ürün kodunun parçasıdır (W6**ER**01Z8): kategoride kodlu ürün oluştuktan sonra kod değiştirilemez;
ürünü olan kategori silinemez. API: `GET /api/categories/manage`, `POST /api/categories`, `PUT /api/categories/{id}`,
`PUT /api/categories/{id}/code`, `DELETE /api/categories/{id}`.

## İşlem kaydı

Panel: **Güvenlik & Sistem → İşlem Kaydı** (Admin tüm mağazalar, mağaza müdürü kendi mağazası; kasiyer erişemez).
API: `GET /api/audit-logs?from=&to=&storeId=&user=&action=&entityId=&succeeded=&page=&pageSize=`

- **İstek kaydı** (`WebAPI/Middleware/AuditMiddleware.cs`): sisteme yazan her istek (POST/PUT/PATCH/DELETE) —
  satış, iade, değişim, yönetici onayı, giriş denemesi, kullanıcı/ürün/kupon işlemleri, fiş yeniden basımı —
  kim (personel no, ad, rol), hangi mağaza, ne zaman, sonuç (başarılı / reddedildi + sunucu mesajı), fiş/belge no,
  IP ve süreyle yazılır. Reddedilen denemeler de kaydedilir.
- **Alan değişikliği kaydı** (`AuditableEntityInterceptor`): ürün, varyant, kategori, kupon, mağaza, iade nedeni,
  müşteri, kullanıcı ve min. stok değişikliklerinde alanın eski → yeni değeri; değişiklikle aynı işlemde yazılır
  ve isteğe `CorrelationId` ile bağlanır.
- **Gizlilik:** PIN, şifre, token ve hash değerleri hiç yazılmaz (`***` / `(değişti)`); kart numarası son 4 hane,
  müşteri telefonu son 2 hane, müşteri e-postası maskelenir (`AuditSanitizer`).
- **Değiştirilemez:** `AuditLogs` tablosundaki `TR_AuditLogs_AppendOnly` tetikleyicisi doğrudan SQL ile yapılan
  UPDATE/DELETE'i de reddeder.

## Yedekleme

Panel: **Güvenlik & Sistem → Yedekler** (yalnızca Admin) — son başarılı yedek, sonraki planlı yedek, geçmiş,
"Şimdi yedekle". Son 26 saatte başarılı yedek yoksa kırmızı uyarı gösterilir.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `Backup:Enabled` | `false` | Günlük otomatik yedek. Canlıda **açın**. |
| `Backup:Directory` | SQL Server varsayılanı | **SQL Server'ın gördüğü** klasör; veritabanından ayrı bir diskte olmalı |
| `Backup:DailyAt` | `02:30` | Sunucu yerel saati |
| `Backup:RetentionDays` / `MinKeep` | `30` / `7` | Bu süreden eski yedekler silinir; en yeni `MinKeep` adet hiç silinmez |

Her yedek `BACKUP ... WITH CHECKSUM` ile alınır ve hemen `RESTORE VERIFYONLY` ile doğrulanır. Sunucu planlı
saatte kapalıysa açılışta (son başarılı yedek 24 saatten eskiyse) telafi yedeği alınır. Yedekler aynı sunucuda
kalırsa disk arızasında birlikte kaybolur: klasörü düzenli olarak başka bir makineye/buluta kopyalayın.

### Yedekten geri dönme

```bash
# 1) API'yi durdurun (kasa işlemleri dursun)
# 2) Mevcut durumun da yedeğini alın (geri dönüşün geri alınabilmesi için)
sqlcmd -S <sunucu> -Q "BACKUP DATABASE LCWaikikiPOS TO DISK='D:\Yedek\LCWaikikiPOS_geri-donus-oncesi.bak' WITH CHECKSUM"
# 3) Geri yükleyin
sqlcmd -S <sunucu> -Q "ALTER DATABASE LCWaikikiPOS SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE LCWaikikiPOS FROM DISK='D:\Yedek\LCWaikikiPOS_<tarih>.bak' WITH REPLACE, CHECKSUM; ALTER DATABASE LCWaikikiPOS SET MULTI_USER"
# 4) API'yi başlatın; yedek eski bir sürümdense bekleyen migration'lar listelenir (bkz. Production'a çıkış)
```

Geri yüklenebilirlik testlerde de doğrulanır: `AuditAndBackupTests` yedeği ayrı bir veritabanına açıp satış
sayısını karşılaştırır.
