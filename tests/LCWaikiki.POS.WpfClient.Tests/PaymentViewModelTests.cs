using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;
using LCWaikiki.POS.WpfClient.ViewModels;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>Odeme ekrani: tutar hesaplari, kart/para ustu kurallari, cift gonderim korumasi.</summary>
[Collection("ApiClient")]
public class PaymentViewModelTests
{
    private static (PaymentViewModel vm, FakeApi api) Create(decimal total)
    {
        var api = new FakeApi();
        var client = api.Client();
        var vm = new PaymentViewModel(new PosService(client), new AuthService(client), new ClientSettings());
        vm.ResetPaymentState();
        vm.SaleItems = [new SaleItemRequest(1, 1, null, null)];
        vm.GrandTotal = total;
        return (vm, api);
    }

    private static void Type(PaymentViewModel vm, string amount)
    {
        vm.ClearAmountCommand.Execute(null);
        foreach (var c in amount) vm.AppendDigitCommand.Execute(c.ToString());
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(20);
    }

    [Fact]
    public void Partial_cash_leaves_remaining_and_no_change()
    {
        var (vm, api) = Create(250m);
        api.Respond = _ => Task.FromResult(FakeApi.Json(200, FakeApi.SaleOk()));
        Type(vm, "100");
        vm.AddCashCommand.Execute(null);

        Assert.Equal(100m, vm.PaidTotal);
        Assert.Equal(150m, vm.RemainingAmount);
        Assert.Equal(0m, vm.ChangeAmount);
        Assert.Empty(api.Requests);           // eksik odemede satis gonderilmez
    }

    [Fact]
    public async Task Cash_over_total_gives_change_and_completes_sale_automatically()
    {
        var (vm, api) = Create(80m);
        api.Respond = _ => Task.FromResult(FakeApi.Json(200, FakeApi.SaleOk()));
        Type(vm, "100");
        vm.AddCashCommand.Execute(null);

        Assert.Equal(20m, vm.ChangeAmount);
        Assert.Equal(0m, vm.RemainingAmount);
        await WaitFor(() => api.Requests.Count == 1);
        Assert.Single(api.Requests);
        Assert.Equal("/api/pos/sales", api.Requests.Single().Path);
    }

    [Fact]
    public void Cash_button_without_amount_does_not_take_whole_total()
    {
        var (vm, _) = Create(250m);
        vm.AddCashCommand.Execute(null);

        Assert.Empty(vm.PaymentLines);
        Assert.Equal(250m, vm.RemainingAmount);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void Card_cannot_be_charged_more_than_remaining()
    {
        var (vm, _) = Create(250m);
        Type(vm, "300");
        vm.PosSaleCommand.Execute(null);

        Assert.Empty(vm.PaymentLines);
        Assert.Contains("kalan tutar", vm.ErrorMessage);
    }

    [Fact]
    public void Exchange_where_customer_is_owed_money_refuses_payment()
    {
        var (vm, _) = Create(-120m);   // iade > yeni satis
        Type(vm, "50");
        vm.AddCashCommand.Execute(null);

        Assert.Equal(120m, vm.AmountOwedToCustomer);
        Assert.False(vm.CanTakePayment);
        Assert.Empty(vm.PaymentLines);
        Assert.Equal(0m, vm.ChangeAmount);
    }

    [Fact]
    public async Task Retry_after_network_error_reuses_key_and_new_payment_session_gets_new_key()
    {
        var (vm, api) = Create(100m);
        var calls = 0;
        api.Respond = _ => Interlocked.Increment(ref calls) == 1
            ? throw new HttpRequestException("ag koptu")
            : Task.FromResult(FakeApi.Json(200, FakeApi.SaleOk()));

        Type(vm, "100");
        vm.AddCashCommand.Execute(null);                     // otomatik tamamlama -> ag hatasi
        await WaitFor(() => !string.IsNullOrEmpty(vm.ErrorMessage));
        Assert.Contains("Sunucuya ulasilamadi", vm.ErrorMessage);

        _ = vm.CompleteSaleCommand.ExecuteAsync(null);       // kasiyer tekrar ✓
        await WaitFor(() => api.Requests.Count == 2);

        var keys = api.Requests.Select(r => r.IdempotencyKey).ToList();
        Assert.Equal(2, keys.Count);
        Assert.False(string.IsNullOrEmpty(keys[0]));
        Assert.Equal(keys[0], keys[1]);

        var oldKey = vm.IdempotencyKey;
        vm.ResetPaymentState();                              // yeni musteri
        Assert.NotEqual(oldKey, vm.IdempotencyKey);
    }

    [Fact]
    public async Task Double_tap_on_complete_sends_sale_once()
    {
        var (vm, api) = Create(100m);
        api.Respond = async _ => { await Task.Delay(300); return FakeApi.Json(200, FakeApi.SaleOk()); };
        Type(vm, "60");
        vm.AddCashCommand.Execute(null);
        vm.PosSaleCommand.Execute(null);                     // kalan 40 kartla -> otomatik tamamlama

        _ = vm.CompleteSaleCommand.ExecuteAsync(null);
        _ = vm.CompleteSaleCommand.ExecuteAsync(null);
        await Task.Delay(800);

        Assert.Single(api.Requests);
        Assert.Equal(40m, vm.PaymentLines.Single(p => p.Type == PaymentType.Card).Amount);
    }
}
