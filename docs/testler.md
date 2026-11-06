# Testler (ayrıntılı)

[← README](../README.md)

## Testler

```bash
dotnet test
```

- **Birim testleri** (`Unit/`): ödeme/para üstü, iade tutarı, telefon normalizasyonu, PIN hash ve kuralları.
- **Canlıya hazırlık testleri** (`Api/HardeningTests.cs`): eşzamanlı hediye kartı harcaması, eşzamanlı satışlarda
  stok tutarlılığı, `Idempotency-Key` (tekrar, eşzamanlı tekrar, hata sonrası tekrar, başka uçta kullanım),
  adet/tutar sınırları, eşzamanlı ürün kodu üretimi. Açılış kontrolleri: `Unit/StartupGuardTests.cs`.
- **Kasa uygulaması testleri** (`tests/LCWaikiki.POS.WpfClient.Tests`, sunucu gerektirmez): oturum süresi dolunca
  PIN ile yeniden giriş ve aynı işlem anahtarıyla tekrar gönderim, ağ hatası/zaman aşımı mesajları ve bağlantı
  göstergesi, ödeme ekranı (para üstü, kart üst sınırı, çift gönderim koruması), sepet satırı hesapları.
- **Entegrasyon testleri** (`Api/`): API'nin tamamı bellek içinde çalışır (kimlik doğrulama, middleware,
  MediatR pipeline, EF Core). Her çalıştırma **benzersiz adlı geçici bir veritabanı** oluşturur ve sonunda siler.
  Her test kendi mağazasını/kullanıcılarını kurar; testler birbirinden bağımsızdır.
  Eşzamanlılık senaryoları (aynı onayla çift kasa açma, aynı fişe eşzamanlı iade, eşzamanlı satışlarda
  fiş numarası) gerçek SQL Server üzerinde sınanır — kod MERGE, rowversion ve filtreli unique index
  kullandığı için SQLite/InMemory yeterli değildir.

SQL Server adresi `POS_TEST_SQL` ortam değişkeniyle değiştirilebilir (varsayılan LocalDB):

```bash
POS_TEST_SQL="localhost,1433;User Id=sa;Password=<şifre>" dotnet test
```
