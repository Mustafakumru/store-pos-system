# Teknik kararlar

[← README](../README.md)

Bu belge, projeyi "çalışan bir CRUD"dan ayıran kararları anlatır. Her başlıkta: **sorun** (çoğu derin testlerle
bulundu), **çözüm**, **kodun yeri** ve sorunun tekrar etmeyeceğini kanıtlayan **test(ler)**.

---

## 1. Para ve stok hareketlerinde eşzamanlılık

**Sorun.** İlk sürümde aynı fişe aynı anda gelen iki iade isteği, ikisi de "iade edilebilir adet = 1" okuyup
müşteriye iki kez para ödüyordu; 100 TL'lik hediye kartı iki kasada aynı anda harcanabiliyordu; eşzamanlı
satışlar aynı stok satırını okuyup üzerine yazıyordu (kayıp güncelleme).

**Çözüm.** Para/stok taşıyan satırlara (`GiftCard`, `StoreInventory`, `SaleItem`, `Coupon`, `Sale`) SQL Server
`rowversion` eklendi (iyimser kilit). Çakışma olursa MediatR pipeline'ındaki `ConcurrencyRetryBehavior` komutu
**baştan, güncel veriyle** yeniden çalıştırır (artan rastgele bekleme, 8 deneme). Yeniden denemede ikinci iade
"iade edilebilir adet 0" görür ve reddedilir; stok satışı ise doğru değerle tamamlanır. Mağaza stok satırının iki
istekte aynı anda *oluşturulması* (unique index ihlali 2601/2627) da çakışma sayılır.

**Kod.** `Application/Common/Behaviors/ConcurrencyRetryBehavior.cs`, `IConcurrencySensitiveCommand` işaretli
komutlar, `ApplicationDbContext.IsConcurrencyConflict`.

**Test.** `Concurrent_double_refund_of_same_item_succeeds_once`,
`Concurrent_refunds_on_same_receipt_cannot_exceed_card_paid_amount`,
`Gift_card_balance_cannot_be_spent_twice_concurrently`, `Concurrent_sales_never_lose_stock_updates`
(15 eşzamanlı satış — 4 deneme yetmediği bu testle bulundu, 8'e çıkarıldı),
`Concurrent_sales_with_mixed_lines_keep_every_variant_consistent`,
`Single_use_coupon_is_used_once_under_concurrency`.

## 2. Tekrar gönderim koruması (`Idempotency-Key`)

**Sorun.** Ağ yanıt gelmeden koptuğunda kasiyer "Ödeme"ye tekrar basar; sunucu ilk isteği zaten işlemişse
**ikinci fiş** kesilir, müşteriden iki kez tahsilat yapılır.

**Çözüm.** Para hareketi yapan uçlar (`[Idempotent]`: satış, değişim, iade, hediye kartı) istemcinin gönderdiği
`Idempotency-Key`'i `IdempotencyRecords` tablosuna (kullanıcı + anahtar tekil) yazar. Aynı anahtar tekrar gelirse
işlem yapılmaz, **ilk yanıt aynen döner** (`Idempotent-Replayed: true`); ilki hâlâ işleniyorsa 409; aynı anahtar
başka bir uçta kullanılırsa 422; istek başarısız olursa kayıt silinir (düzeltilmiş tekrar çalışır). Kasa uygulaması
anahtarı ödeme oturumu başına bir kez üretir: aynı ödemede kaç kez basılırsa basılsın aynı anahtar gider.

**Kod.** `WebAPI/Common/IdempotentAttribute.cs`, `PaymentViewModel.IdempotencyKey`.

**Test.** `Idempotency_key_replays_the_first_response`, `Concurrent_requests_with_same_key_create_one_sale`,
`Failed_request_releases_key_so_corrected_retry_succeeds`, `Key_reused_on_a_different_endpoint_is_rejected`,
`Gift_card_sale_retried_with_same_key_loads_balance_once`,
`Retry_after_network_error_reuses_key_and_new_payment_session_gets_new_key`, `Double_tap_on_complete_sends_sale_once`.

## 3. Boşluksuz ve eşzamanlı güvenli fiş numarası

**Sorun.** Fiş numarası doğrulamalardan önce alındığı için başarısız her satış bir numara "yakıyordu"
(0044 → 0047); `MAX()+1` ile eşzamanlı satışlar aynı numarayı alabiliyordu.

**Çözüm.** Mağaza + gün bazında sayaç tablosu, `MERGE ... WITH (HOLDLOCK)` ile atomik artış; numara **tüm
doğrulamalar geçtikten sonra**, kayıtla **aynı transaction'da** alınır — kayıt başarısız olursa sayaç da geri alınır.
Biçim yalnızca rakam (`{mağaza:000}{yyyyMMdd}{sıra:0000}`), fiş barkod olarak okutulabilsin diye.

**Kod.** `ApplicationDbContext.GetNextReceiptSequenceValueAsync`, `ProcessSaleCommandHandler`.

**Test.** `Failed_sales_do_not_burn_receipt_numbers`, `Concurrent_sales_get_unique_consecutive_receipt_numbers`.

## 4. Yönetici onayı: tek kullanımlık, kapsamlı token

**Sorun.** İndirim, fişsiz iade, kasa açılışı gibi işlemler müdür onayı ister. Onay "evet/hayır" olarak tutulursa
tekrar oynatılabilir, başka kasiyere ya da başka işleme taşınabilir.

**Çözüm.** Müdür PIN'iyle alınan kısa ömürlü, **işleme ve isteyen kasiyere bağlı**, tek kullanımlık onay token'ı.
İşlem başarıyla kaydedildikten sonra tüketilir (başarısız işlemde kasiyer aynı onayla tekrar deneyebilir).
Onay token'ı normal oturum token'ı olarak kullanılamaz; başka mağazanın müdürü onay veremez; varsayılan PIN'le onay
verilemez.

**Test.** `Discount_requires_single_use_manager_approval`, `Concurrent_replay_of_one_discount_approval_succeeds_once`,
`Approval_of_another_cashier_is_rejected`, `Approval_token_cannot_be_used_as_bearer_token`,
`Manager_of_another_store_cannot_approve`, `Same_approval_token_used_concurrently_opens_exactly_one_session`.

## 5. Çevrimdışı satış

**Sorun.** Mağazada internet kesildiğinde kasa durmamalı; ama "sonra gönderilecek" satış çift kayda, uydurma fiyata
ya da başka kasiyerin adına yazılmaya açıktır.

**Çözüm.**
- Kasa, mağazanın ürün/fiyat listesini düzenli indirip diske yazar; sunucu yoksa barkod yerelden okunur.
- Gönderilemeyen satış diske (atomik yazımla) kuyruğa alınır; **ilk denemenin `Idempotency-Key`'i korunur** —
  istek aslında sunucuya ulaşmışsa gönderimde aynı fiş döner. Ek savunma: `(StoreId, OfflineReference)` tekil index.
- Sunucu fiyatı **satışın kasada yapıldığı ana** göre hesaplar; kasanın kataloğu eskiyse yalnızca son 48 saatte
  gerçekten geçerli olmuş bir fiyatı kabul eder, hiç geçerli olmamış fiyatı reddeder.
- Sunucu onayı gerektiren hiçbir şey çevrimdışı yapılamaz (kupon, hediye kartı, indirim, iade).
- Satış yalnızca onu yapan kasiyerin oturumuyla gönderilir; gönderilmemiş satış varken Z raporu alınamaz.

**Kod.** `Core/Offline/*` (kasa), `Application/Features/POS/Common/OfflineSales.cs` (sunucu).

**Test.** `Network_failure_saves_sale_offline_with_same_key_and_prints_offline_number`,
`Same_offline_document_sent_twice_creates_one_sale`,
`Price_at_sale_time_is_used_and_recent_old_price_is_accepted_but_invented_price_is_not`,
`Another_cashiers_sale_waits_for_that_cashier`, `Network_error_or_expired_session_keeps_sales_pending_without_pin_popup`,
`Customer_can_return_using_the_offline_document_number_on_their_receipt`; ayrıca gerçek uygulamada uçtan uca
(sunucu kapatıldı → satış → sunucu açıldı → otomatik gönderim) denendi.

## 6. Oturum süresi dolunca veri kaybetmemek

**Sorun.** JWT süresi dolduğunda her istek 401 alıyor, kasiyer sepetini kaybedip baştan giriş yapıyordu.

**Çözüm.** Tüm istekler tek noktadan (`ApiClient.SendAsync`) geçer. 401'in **süresi dolmuş token**'dan mı
(`WWW-Authenticate: Bearer error="invalid_token"`) yoksa iş kuralından mı (yanlış müdür PIN'i) geldiği ayırt edilir;
yalnızca ilkinde PIN penceresi açılır, başarılıysa bekleyen istek **aynı işlem anahtarıyla** tekrar gönderilir.
Aynı anda süresi dolan birden çok istek tek PIN sorusunda birleşir; arka plan işleri pencere açmaz.

**Test.** `Expired_token_triggers_reauth_and_retries_with_same_idempotency_key`,
`Business_401_like_wrong_manager_pin_does_not_ask_for_relogin`, `Concurrent_expired_requests_ask_for_pin_only_once`.

## 7. Değiştirilemez işlem kaydı

**Çözüm.**
- **İstek kaydı:** boru hattının en dışındaki ara katman, sisteme yazan her isteği (başarılı ya da reddedilen) kim /
  mağaza / zaman / sonuç / fiş no ile yazar — **yanıttan önce** (istemci "başarılı" gördüğünde kayıt yazılmıştır).
- **Alan değişikliği:** EF Core interceptor'ı ana verilerde alanın eski → yeni değerini **aynı `SaveChanges`** içinde
  yazar (değişiklikle birlikte kaydedilir ya da hiç yazılmaz) ve isteğe `CorrelationId` ile bağlar.
- **Gizlilik:** PIN, şifre, token, hash asla yazılmaz; kart no son 4 hane, müşteri telefonu/e-postası maskeli.
- **Değiştirilemezlik:** `INSTEAD OF UPDATE, DELETE` tetikleyicisi doğrudan SQL ile silmeyi de reddeder.

**Test.** `Audit_records_cannot_be_updated_or_deleted_even_with_direct_sql`,
`Failed_pos_login_is_recorded_with_personel_id_but_never_the_pin`,
`Approval_and_gift_card_records_hide_pin_token_and_card_number`,
`Coupon_deactivation_records_old_and_new_value_linked_to_request`,
`Manager_sees_only_own_store_cashier_is_forbidden_admin_sees_all`.

## 8. Yedeklemede "alındı" değil "geri dönülebilir"

**Çözüm.** `BACKUP ... WITH CHECKSUM`, ardından `RESTORE VERIFYONLY`; aynı anda tek yedek (`sp_getapplock`);
saklama süresi temizliği en yeni N yedeğe dokunmaz; sunucu planlı saatte kapalıysa açılışta telafi yedeği.

**Test.** `Admin_backup_is_verified_restorable_and_recorded_others_forbidden` — yedeği **ayrı bir veritabanına geri
yükleyip** satış sayısını karşılaştırır.

## 9. Hatalı yapılandırmayla açılmayan API

**Sorun.** Geliştirme JWT anahtarı depoda açık; canlıda bu anahtarla çalışan API'de herkes geçerli admin token'ı
üretebilir. Benzer şekilde `*` CORS ya da bekleyen migration'la eski şemaya yazma.

**Çözüm.** Açılış kontrolleri: depodaki anahtarın SHA-256 özeti geliştirme dışında reddedilir; canlıda panel adresi
`https://` olmalı; canlıda migration otomatik uygulanmaz, bekleyen varsa API açılmaz.

**Test.** `Public_dev_key_is_allowed_only_in_development`, `Missing_or_short_key_is_rejected`,
`Production_requires_explicit_https_panel_origin`.

## 10. Termal fiş: metin değil raster

**Sorun.** ESC/POS'ta metin göndermek yazıcının kod sayfasına bağlıdır; Türkçe karakterler (ğ, ş, İ) yazıcıdan
yazıcıya bozulur ve fiş ekrandaki/PDF'teki görünümden farklı olur.

**Çözüm.** Fişin WPF görseli yazıcı çözünürlüğünde (203 dpi, 80 mm = 576 nokta) 1 bit raster'a çevrilir ve
`GS v 0` bantlarıyla (bazı yazıcıların sınırı olan 256 satırlık parçalar) gönderilir, ardından besleme + kesme.
Yazdırma hatası satışı bozmaz; belge önce PDF olarak arşivlenir.

**Test.** `Real_receipt_is_printed_as_raster_at_paper_width` — gerçek bir fiş yazıcı baytlarına çevrilip **geri
çözülür**; README'deki fiş görseli bu çözümlemeden üretilmiştir. `Raster_is_split_into_bands_with_correct_headers`.

## 11. Test stratejisi: InMemory değil gerçek SQL Server

Yukarıdaki çözümlerin çoğu veritabanı davranışına dayanır (`MERGE ... HOLDLOCK`, `rowversion`, filtreli unique
index, tetikleyici, `BACKUP/RESTORE`). EF InMemory ya da SQLite bunları sınayamaz; bu yüzden entegrasyon testleri
`WebApplicationFactory` ile API'nin tamamını (kimlik doğrulama, ara katmanlar, MediatR pipeline) **benzersiz adlı
geçici bir SQL Server veritabanına** karşı çalıştırır, bitince siler. Her test kendi mağazasını ve kullanıcılarını
kurar; testler sırasız ve bağımsızdır. CI'da SQL Server 2022 konteyneri kullanılır.

Kasa uygulaması testleri sunucu gerektirmez: `ApiClient` sahte bir `HttpMessageHandler` ile beslenir (oturum
yenileme, ağ hatası, zaman aşımı, çevrimdışı kuyruk senaryoları).
