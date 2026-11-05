using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WebAPI.Common;

namespace LCWaikiki.POS.IntegrationTests.Unit;

/// <summary>API açılış kontrolleri: hatalı yapılandırmayla production'da açılmamalı.</summary>
public class StartupGuardTests
{
    private const string PublicDevKey = "LCWaikiki-POS-Super-Secret-Key-2024-MinLength32Chars!";
    private const string StrongKey = "T3st-Only-Strong-Random-Key-8f2c41d9a77e4b0c9e15aa2b";

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "WebAPI";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static readonly Env Dev = new(Environments.Development);
    private static readonly Env Prod = new(Environments.Production);

    private static IConfiguration Origins(params string[] origins)
        => new ConfigurationBuilder().AddInMemoryCollection(
            origins.Select((o, i) => new KeyValuePair<string, string?>($"AllowedOrigins:{i}", o))).Build();

    [Fact]
    public void Public_dev_key_is_allowed_only_in_development()
    {
        Assert.NotEmpty(StartupGuards.ValidateJwtSecret(PublicDevKey, Dev));
        var ex = Assert.Throws<InvalidOperationException>(() => StartupGuards.ValidateJwtSecret(PublicDevKey, Prod));
        Assert.Contains("GELİŞTİRME", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kisa-anahtar")]
    public void Missing_or_short_key_is_rejected(string? key)
        => Assert.Throws<InvalidOperationException>(() => StartupGuards.ValidateJwtSecret(key, Prod));

    [Fact]
    public void Strong_key_is_accepted_in_production()
        => Assert.NotEmpty(StartupGuards.ValidateJwtSecret(StrongKey, Prod));

    [Fact]
    public void Production_requires_explicit_https_panel_origin()
    {
        Assert.Throws<InvalidOperationException>(() => StartupGuards.ValidateAllowedOrigins(Origins(), Prod));
        Assert.Throws<InvalidOperationException>(() => StartupGuards.ValidateAllowedOrigins(Origins("*"), Prod));
        Assert.Throws<InvalidOperationException>(() => StartupGuards.ValidateAllowedOrigins(Origins("http://panel.test"), Prod));
        Assert.Equal(["https://panel.test"], StartupGuards.ValidateAllowedOrigins(Origins("https://panel.test"), Prod));
    }

    [Fact]
    public void Development_allows_local_http_panel()
        => Assert.Single(StartupGuards.ValidateAllowedOrigins(Origins("http://localhost:4200"), Dev));
}
