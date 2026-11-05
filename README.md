# LC Waikiki POS

Mağaza kasa (POS) sistemi: satış, iade/değişim, hediye kartı, kasa oturumu (avans/teslim),
X/Z raporları, depo → mağaza sevkiyatı ve yönetim paneli.

| Bileşen | Teknoloji | Klasör |
|---|---|---|
| API | ASP.NET Core 8, Clean Architecture, MediatR (CQRS), EF Core 8, SQL Server | `Domain`, `Application`, `Infrastructure`, `WebAPI` |
| Yönetim / mağaza paneli | Angular 17, PrimeNG, Tailwind | `lcw-pos-admin` |
| Kasa uygulaması | WPF (.NET 8), CommunityToolkit.Mvvm | `LCWaikiki.POS.WpfClient` |
| Testler | xUnit, WebApplicationFactory, gerçek SQL Server | `tests/LCWaikiki.POS.IntegrationTests` |

## Çalıştırma (geliştirme)

```bash
# API — açılışta migration + geliştirme seed'i (admin@lcwaikiki.com / Admin123!, test kasiyeri 1000001 / 1234)
dotnet run --project WebAPI --launch-profile https      # https://localhost:7164/swagger

# Panel
cd lcw-pos-admin && npm install && npx ng serve         # http://localhost:4200

# Kasa uygulaması
dotnet run --project LCWaikiki.POS.WpfClient
```

Varsayılan veritabanı: `(localdb)\mssqllocaldb` / `LCWaikikiPOS`.

## Testler

```bash
dotnet test
```

- **Birim testleri** (`Unit/`): ödeme/para üstü, iade tutarı, telefon normalizasyonu, PIN hash ve kuralları.
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

## Yapılandırma ve gizli bilgiler

| Ayar | Geliştirme | Production |
|---|---|---|
| `JwtSettings:SecretKey` (≥ 32 bayt) | `appsettings.Development.json` | `JwtSettings__SecretKey` ortam değişkeni / anahtar kasası — **tanımsızsa API açılmaz** |
| `Seed:AdminPassword` | gerekmez (`Admin123!`) | ilk admin için zorunlu; test mağazası/kasiyeri oluşturulmaz |
| `RateLimiting:PerUserPerMinute` / `PerIpPerMinute` | 20 / 120 | 20 / 120 |
