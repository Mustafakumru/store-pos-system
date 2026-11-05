using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Auth;
using LCWaikiki.POS.WpfClient.Core;

namespace LCWaikiki.POS.WpfClient.Core.Services;

public class AuthService(ApiClient api)
{
    private readonly ApiClient _api = api;

    public LoginResponse? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;

    /// <summary>
    /// Kasiyer PersonelId + PIN ile giriş yapar.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        string personelId, string pin, CancellationToken ct = default)
    {
        // Suresi dolmus eski token gonderilmesin (yeniden giris dahil)
        _api.ClearToken();
        var request = new PosLoginRequest(personelId, pin);
        var response = await _api.PostAsync<LoginResponse>(
            "api/pos/auth/login", request, ct);

        CurrentUser = response;
        _api.SetToken(response.Token);
        AppLog.Context = $"Kasiyer {response.PersonelId} / Magaza {response.StoreId}";
        AppLog.Info("Giris yapildi.");
        return response;
    }

    /// <summary>
    /// PIN degistirir (ilk giriste zorunlu). Backend tam yetkili yeni token
    /// doner - ilk giris token'i ("mustChangePin") sadece bu cagriya yarar.
    /// </summary>
    public async Task ChangePinAsync(string oldPin, string newPin, CancellationToken ct = default)
    {
        var user = CurrentUser ?? throw new InvalidOperationException("Oturum yok.");
        var response = await _api.PostAsync<ChangePinResponse>(
            "api/pos/auth/change-pin",
            new { personelId = user.PersonelId, oldPin, newPin }, ct);

        _api.SetToken(response.Token);
        CurrentUser = user with { Token = response.Token, IsFirstLogin = false };
    }

    /// <summary>
    /// Yönetici onayı için kısa ömürlü token alır.
    /// </summary>
    public async Task<ApprovalTokenResponse> GetApprovalTokenAsync(
        string personelId, string pin, string action, CancellationToken ct = default)
    {
        var request = new { personelId, pin, action };
        var response = await _api.PostAsync<ApprovalTokenResponse>(
            "api/pos/manager-approval", request, ct);
        return response;
    }

    public void Logout()
    {
        CurrentUser = null;
        _api.ClearToken();
    }
}

public record ApprovalTokenResponse(string ApprovalToken, int ExpiresInSeconds);

public record ChangePinResponse(string Message, string Token);