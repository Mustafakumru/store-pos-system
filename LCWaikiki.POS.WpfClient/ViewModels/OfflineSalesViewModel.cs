using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Offline;
using LCWaikiki.POS.WpfClient.Core.Services;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public record OfflineSaleRow(OfflineSale Sale, string Status)
{
    public string Reference => Sale.Reference;
    public string SoldAt => Sale.SoldAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public string Amount => $"{Sale.FinalAmount:N2} TL";
    public string Cashier => $"{Sale.CashierName} #{Sale.CashierPersonelId}";
    public string Note => Sale.LastError ?? (Sale.ServerReceiptNumber is { } r ? $"Fis {r}" : "");
}

/// <summary>
/// Ana Menu > Cevrimdisi Satislar: bekleyen / reddedilen satislar, "Simdi gonder",
/// reddedileni "Tekrar dene" ya da yonetici onayiyla "Arsivle".
/// </summary>
public partial class OfflineSalesViewModel : BaseViewModel
{
    public const string DismissApprovalAction = "offline-sale-dismiss";
    private readonly OfflineSaleQueue _queue;
    private readonly OfflineSyncService _sync;
    private readonly AuthService _auth;

    public OfflineSalesViewModel(OfflineSaleQueue queue, OfflineSyncService sync, AuthService auth)
    {
        _queue = queue;
        _sync = sync;
        _auth = auth;
    }

    public OfflineSaleQueue Queue => _queue;
    public ObservableCollection<OfflineSaleRow> Rows { get; } = new();

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _info = string.Empty;
    [ObservableProperty] private OfflineSaleRow? _selected;
    [ObservableProperty] private string _managerPersonelId = string.Empty;
    [ObservableProperty] private string _managerPin = string.Empty;

    [RelayCommand]
    private void Open()
    {
        Reload();
        Info = string.Empty;
        ClearError();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            var r = await _sync.SyncNowAsync();
            Info = r.StoppedBecause is null
                ? $"{r.Sent} satis gonderildi, {r.Failed} reddedildi. Bekleyen: {r.Remaining}."
                : $"{r.Sent} satis gonderildi. Durdu: {r.StoppedBecause}";
        }
        finally
        {
            IsBusy = false;
            Reload();
        }
    }

    [RelayCommand]
    private void Retry()
    {
        if (Selected is not { Status: "Reddedildi" } row) { SetError("Reddedilen bir satis secin."); return; }
        _queue.RetryFailed(row.Sale);
        Info = $"{row.Reference} yeniden gonderim sirasina alindi.";
        Reload();
    }

    [RelayCommand]
    private async Task DismissAsync()
    {
        if (Selected is not { Status: "Reddedildi" } row) { SetError("Reddedilen bir satis secin."); return; }
        if (string.IsNullOrWhiteSpace(ManagerPersonelId) || string.IsNullOrWhiteSpace(ManagerPin))
        {
            SetError("Arsivlemek icin magaza muduru kodu ve PIN gerekli.");
            return;
        }
        try
        {
            IsBusy = true;
            await _auth.GetApprovalTokenAsync(ManagerPersonelId.Trim(), ManagerPin, DismissApprovalAction);
            _queue.Dismiss(row.Sale, ManagerPersonelId.Trim());
            Info = $"{row.Reference} arsivlendi (onaylayan {ManagerPersonelId.Trim()}).";
            ManagerPin = string.Empty;
            ClearError();
        }
        catch (ApiException ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            Reload();
        }
    }

    public void Reload()
    {
        Rows.Clear();
        foreach (var s in _queue.Failed()) Rows.Add(new OfflineSaleRow(s, "Reddedildi"));
        foreach (var s in _queue.Pending()) Rows.Add(new OfflineSaleRow(s, "Bekliyor"));
        foreach (var s in _queue.Synced().OrderByDescending(s => s.SyncedAtUtc).Take(20)) Rows.Add(new OfflineSaleRow(s, "Gonderildi"));
        _queue.RefreshCounts();
    }
}
