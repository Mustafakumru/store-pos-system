using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Core.Services;

public class CustomerService
{
    private readonly ApiClient _apiClient;

    public CustomerService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Telefon numarasiyla musteri arar. Bulunamazsa null doner
    /// (backend 200+null veya 404 donebilir, ikisi de guvenli sekilde ele alinir).
    /// </summary>
    public async Task<CustomerDto?> SearchByPhoneAsync(string phone, CancellationToken ct = default)
    {
        try
        {
            return await _apiClient.GetAsync<CustomerDto?>(
                $"api/pos/customers/search?phone={Uri.EscapeDataString(phone)}", ct);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Yeni musteri kaydi olusturur.
    /// </summary>
    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
        => await _apiClient.PostAsync<CustomerDto>("api/pos/customers", request, ct);

    /// <summary>
    /// Musterinin BELIRLI BIR URUNU (barkod) gecmiste alip almadigini kontrol eder.
    /// Degisim akisinda kullanilir. Barkod zorunludur - musterinin tum gecmisini
    /// cekmek mumkun degildir (operasyonel/mahremiyet kisitlamasi backend'de).
    /// </summary>
    public async Task<List<CustomerPurchaseDto>> GetPurchaseHistoryAsync(
        int customerId, string barcode, CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<List<CustomerPurchaseDto>>(
            $"api/pos/customers/{customerId}/purchases?barcode={Uri.EscapeDataString(barcode)}", ct);
    }
}