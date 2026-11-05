using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Core.Services;

public class ProductService(ApiClient api)
{
	private readonly ApiClient _api = api;

	/// <summary>
	/// WPOS "Ürün Okutma" — barkod ile ürün/fiyat/stok bilgisi getirir.
	/// StoreId backend'de JWT'den otomatik alınır.
	/// </summary>
	public Task<ProductForPosDto> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
		=> _api.GetAsync<ProductForPosDto>($"api/pos/product/{barcode}", ct);

	/// <summary>Urun kodu (W6ER01Z8) ile urun + renk/beden secenekleri (fiyat, magaza stogu).</summary>
	public Task<ProductByCodeDto> GetByCodeAsync(string code, CancellationToken ct = default)
		=> _api.GetAsync<ProductByCodeDto>($"api/pos/products/by-code/{Uri.EscapeDataString(code.Trim())}", ct);

	/// <summary>Kod parcasi veya ad ile arama (en fazla 20).</summary>
	public Task<List<ProductSearchItemDto>> SearchAsync(string term, CancellationToken ct = default)
		=> _api.GetAsync<List<ProductSearchItemDto>>($"api/pos/products/search?q={Uri.EscapeDataString(term.Trim())}", ct);
}