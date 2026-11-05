using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums;

public enum StockAlertLevel
{
    Normal = 0,
    Low = 1,
    OutOfStock = 2,
    Critical = 3   // Negatif stok
}