using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }
    string? StoreId { get; }    // Kasiyer JWT'sinde "storeId" claim'i
    string? StoreName { get; }
    bool IsAuthenticated { get; }
}