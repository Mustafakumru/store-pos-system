using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }   // JWT'den interceptor yazar

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }   // JWT'den interceptor yazar

    public bool IsDeleted { get; set; } = false; // Soft delete flag
}