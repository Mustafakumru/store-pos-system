namespace LCWaikiki.POS.WpfClient.Core.Models.Auth;

public record LoginResponse(
    string Token,
    string FullName,
    string PersonelId,
    int StoreId,
    bool IsFirstLogin,
    List<string> Roles
);

public record PosLoginRequest(string PersonelId, string Pin);