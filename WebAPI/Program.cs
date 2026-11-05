using System.Text;
using Application;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebAPI.Middleware;
using WebAPI.Common;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────
// 1. KATMAN SERVİSLERİ
// ─────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ─────────────────────────────────────────────
// 2. JWT SERVİSİ
// ─────────────────────────────────────────────
builder.Services.AddScoped<JwtService>();

// ─────────────────────────────────────────────
// 3. JWT AUTHENTICATION
// ─────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// İmza anahtarı kaynak koda / appsettings.json'a YAZILMAZ (eskiden açıkta duruyordu:
// depoya erişen herkes geçerli Admin token'ı üretebilirdi). Geliştirmede
// appsettings.Development.json / user-secrets, production'da ortam değişkeni
// (JwtSettings__SecretKey) ya da gizli anahtar kasası kullanılır.
// Depodaki geliştirme anahtarı geliştirme dışında reddedilir (bkz. StartupGuards).
var secretKey = StartupGuards.ValidateJwtSecret(jwtSettings["SecretKey"], builder.Environment);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero
    };

    // İmza/süre geçerli olsa bile: kullanıcı pasifse, güvenlik damgası
    // değiştiyse ya da bu bir yönetici onay token'ıysa reddet.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = UserTokenValidation.OnTokenValidated
    };
});

builder.Services.AddMemoryCache();

builder.Services.AddAuthorization();

// ─────────────────────────────────────────────
// 4. HTTP CONTEXT — Interceptor için zorunlu
// ─────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ─────────────────────────────────────────────
// 5. CORS — Angular geliştirme sunucusu için
// ─────────────────────────────────────────────
var allowedOrigins = StartupGuards.ValidateAllowedOrigins(builder.Configuration, builder.Environment);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─────────────────────────────────────────────
// 6. CONTROLLERS & SWAGGER
// ─────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        // Tüm tarihler JSON'da UTC ("Z" ekli) - bkz. UtcDateTimeJsonConverter
        o.JsonSerializerOptions.Converters.Add(new WebAPI.Common.UtcDateTimeJsonConverter()));
builder.Services.AddEndpointsApiExplorer();

// PIN/şifre denenen uçlar için IP başına istek sınırı
builder.Services.AddPinRateLimiting();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LC Waikiki POS API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token giriniz. Örnek: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            []
        }
    });
});

// ─────────────────────────────────────────────
var app = builder.Build();

// ─────────────────────────────────────────────
// 7. MİGRASYON & SEED
// ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    // Geliştirmede otomatik; production'da bekleyen migration varsa API açılmaz
    // (Database:AutoMigrate ile değiştirilebilir - bkz. StartupGuards)
    await StartupGuards.ApplyMigrationPolicyAsync(db, app.Configuration, app.Environment,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup"));

    // Rolleri ve admin kullanıcısını oluştur
    // Production'da test verisi / bilinen şifreli admin oluşturulmaz
    await IdentitySeeder.SeedAsync(
        app.Services,
        isDevelopment: app.Environment.IsDevelopment(),
        adminPassword: app.Configuration["Seed:AdminPassword"]);

    // Urun kodu sistemi: kodsuz kategori/urunleri tamamlar (idempotent)
    await Infrastructure.Persistence.ProductCatalogBackfill.RunAsync(app.Services);
}

// ─────────────────────────────────────────────
// 8. MİDDLEWARE SIRASI — Sıra kritik!
// ─────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LC Waikiki POS API v1");
        c.RoutePrefix = "swagger";
    });
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    // Tarayıcı bu alan adına bir daha yalnızca HTTPS ile gelsin
    app.UseHsts();
}

// İşlem kaydı EN DIŞTA: hata ara katmanının ürettiği son durum kodunu ve
// kimlik doğrulamanın doldurduğu kullanıcıyı görür (bkz. AuditMiddleware)
app.UseMiddleware<AuditMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AngularPolicy");
// Authentication → Authorization sırası değişmemeli
app.UseAuthentication();
// PIN'i değişmemiş hesap sadece PIN değiştirebilir (bkz. MustChangePinMiddleware)
app.UseMiddleware<MustChangePinMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Saglik kontrolu: kasa uygulamasinin baglanti gostergesi kullanir (kimlik
// gerektirmez, veritabanina dokunmaz - sadece API ayakta mi).
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", serverTimeUtc = DateTime.UtcNow }))
   .AllowAnonymous();

app.Run();

/// <summary>Entegrasyon testleri (WebApplicationFactory&lt;Program&gt;) için görünür giriş noktası.</summary>
public partial class Program { }
