using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities;

public class StockTransfer : BaseEntity
{
    // Otomatik üretilen belge numarası — örn: "TRF-20250507-0001"
    public string TransferNumber { get; set; } = string.Empty;

    public int ToStoreId { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.Draft;

    // Durum geçiş tarihleri
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public Store ToStore { get; set; } = null!;
    public ICollection<StockTransferItem> Items { get; set; } = [];
}