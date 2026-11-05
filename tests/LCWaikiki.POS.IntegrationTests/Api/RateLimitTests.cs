using LCWaikiki.POS.IntegrationTests.Infrastructure;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>Üretimdeki sınırlarla (kullanıcı başına 20/dk, IP başına 120/dk) ayrı sunucu.</summary>
public sealed class ProductionRateLimitFactory : PosApiFactory
{
    protected override int PerUserRateLimit => 20;
    protected override int PerIpRateLimit => 120;
}

[CollectionDefinition(Name)]
public class RateLimitCollection : ICollectionFixture<ProductionRateLimitFactory>
{
    public const string Name = "rate-limit";
}

[Collection(RateLimitCollection.Name)]
public class RateLimitTests
{
    private readonly ProductionRateLimitFactory _factory;
    public RateLimitTests(ProductionRateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task Limit_is_per_user_so_one_cashier_cannot_block_the_store()
    {
        var s = await Scenario.CreateAsync(_factory);
        async Task<int> RequestApproval(PosUser requester) => (await s.Api.Post("/api/pos/manager-approval",
            new { personelId = s.Manager.PersonelId, pin = s.Manager.Pin, action = "discount" }, requester.Token)).Code;

        var codes = new List<int>();
        for (var i = 0; i < 22; i++) codes.Add(await RequestApproval(s.Cashier));

        Assert.Contains(429, codes);
        Assert.Equal(200, await RequestApproval(s.Manager));   // aynı IP'deki başka kullanıcı etkilenmez
    }
}
