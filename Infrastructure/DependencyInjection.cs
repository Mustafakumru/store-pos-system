using Application.Common.Interfaces;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // HttpContextAccessor — Interceptor'ın JWT okuyabilmesi için
        services.AddHttpContextAccessor();

        // Interceptor — Scoped olmalı
        services.AddScoped<AuditableEntityInterceptor>();

        // DbContext — Interceptor'ı constructor'dan değil,
        // OnConfiguring'den alıyoruz, bu yüzden önce Scoped kayıt şart
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("Infrastructure"));
        });

        // IApplicationDbContext → ApplicationDbContext
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Veritabanı yedeği: servis + günlük zamanlayıcı (Backup:Enabled)
        services.AddSingleton<Infrastructure.Backup.DatabaseBackupService>();
        services.AddHostedService<Infrastructure.Backup.BackupHostedService>();
        services.AddSingleton<ApprovalTokenUsageStore>();
        services.AddScoped<IApprovalTokenService, ApprovalTokenService>();
        // ASP.NET Core Identity
        services.AddIdentity<AppUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.User.RequireUniqueEmail = true;

            // Deneme-yanılma koruması: 5 hatalı şifre/PIN → 15 dk kilit.
            // Panel şifresi (CheckPasswordSignInAsync) ve POS PIN'i (PinSecurity)
            // aynı sayaçları kullanır.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }
}