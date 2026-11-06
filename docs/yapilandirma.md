# Yapılandırma, canlıya alma ve CI

[← README](../README.md)

## Yapılandırma ve gizli bilgiler

| Ayar | Geliştirme | Production |
|---|---|---|
| `JwtSettings:SecretKey` (≥ 32 bayt) | `appsettings.Development.json` | `JwtSettings__SecretKey` ortam değişkeni / anahtar kasası — **tanımsız, kısa ya da depodaki geliştirme anahtarıysa API açılmaz** |
| `Seed:AdminPassword` | gerekmez (`Admin123!`) | ilk admin için zorunlu; test mağazası/kasiyeri oluşturulmaz |
| `RateLimiting:PerUserPerMinute` / `PerIpPerMinute` | 20 / 120 | 20 / 120 |
| `AllowedOrigins` (panel adresi, CORS) | `http(s)://localhost:4200` | `AllowedOrigins__0=https://panel...` — **boş, `*` ya da `http://` ise API açılmaz** |
| `Database:AutoMigrate` | açık (varsayılan) | kapalı (varsayılan) — bekleyen migration varsa API açılmaz |
| `AllowedHosts` | `*` | API'nin alan adı |

Örnek production ayarları: `WebAPI/appsettings.Production.example.json` (gerçek değerler dosyaya değil ortam değişkenine yazılır; `appsettings.Production.json` git'e girmez).

### Production'a çıkış (veritabanı)

Şema değişiklikleri API açılışında kendiliğinden uygulanmaz: önce yedek alınır, sonra tek noktadan uygulanır.

```bash
# 1) Yedek (SQL Server)
sqlcmd -S <sunucu> -Q "BACKUP DATABASE LCWaikikiPOS TO DISK='C:\Yedek\LCWaikikiPOS_<tarih>.bak'"
# 2) Migration'ları uygula (ya da: dotnet ef migrations bundle ile tek dosya üretip sunucuda çalıştır)
dotnet ef database update --project Infrastructure --startup-project WebAPI
# 3) API'yi başlat
```

Production'da ayrıca: HTTPS zorunlu (HSTS açık), Swagger kapalı, hata yanıtlarında iç ayrıntı yok.

## CI (GitHub Actions)

`.github/workflows/ci.yml` — her `main` push'unda ve pull request'te üç paralel iş:

| İş | Runner | Ne yapar |
|---|---|---|
| API testleri | ubuntu-latest + SQL Server 2022 servis konteyneri | Birim + entegrasyon testlerinin tamamı; sonuçlar (`.trx`) artifact olarak yüklenir |
| Kasa uygulaması (WPF) | windows-latest | WPF projesini Release derler + kasa uygulaması testleri |
| Yönetim paneli | ubuntu-latest, Node 20 | `npm ci` + Angular production derlemesi |
