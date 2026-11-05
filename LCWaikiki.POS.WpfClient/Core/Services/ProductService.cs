using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Offline;

namespace LCWaikiki.POS.WpfClient.Core.Services;

public class ProductService(ApiClient api, OfflineCatalog? offlineCatalog = null)
{
	private readonly ApiClient _api = api;
	private readonly OfflineCatalog? _offline = offlineCatalog;

	/// <summary>
	/// WPOS "Ürün Okutma" — barkod ile ürün/fiyat/stok bilgisi getirir.
	/// StoreId backend'de JWT'den otomatik alınır.
	/// </summary>
	/// Sunucuya ulasilamazsa yerel katalogdan doner (FromOfflineCatalog=true, stok bilinmiyor).
	public async Task<ProductForPosDto> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
	{
		// Baglanti zaten yok: 30 sn zaman asimi beklemeden dogrudan yerel katalog
		if (_offline is not null && !ConnectivityMonitor.Instance.IsOnline && _offline.FindForPos(barcode) is { } cachedFirst)
			return cachedFirst;
		try
		{
			return await _api.GetAsync<ProductForPosDto>($"api/pos/product/{barcode}", ct);
		}
		catch (ApiException ex) when (ex.StatusCode == 0 && _offline is not null)
		{
			return _offline.FindForPos(barcode)
				?? throw new ApiException(0, "Sunucuya ulasilamiyor ve urun cevrimdisi katalogda yok. " +
					"Baglanti gelince tekrar okutun.");
		}
	}

	/// <summary>Urun kodu (W6ER01Z8) ile urun + renk/beden secenekleri (fiyat, magaza stogu).</summary>
	public Task<ProductByCodeDto> GetByCodeAsync(string code, CancellationToken ct = default)
		=> _api.GetAsync<ProductByCodeDto>($"api/pos/products/by-code/{Uri.EscapeDataString(code.Trim())}", ct);

	/// <summary>Kod parcasi veya ad ile arama (en fazla 20).</summary>
	public Task<List<ProductSearchItemDto>> SearchAsync(string term, CancellationToken ct = default)
		=> _api.GetAsync<List<ProductSearchItemDto>>($"api/pos/products/search?q={Uri.EscapeDataString(term.Trim())}", ct);
}