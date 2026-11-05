using Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public string UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public string? StoreId =>
        User?.FindFirstValue("storeId");

    public string? StoreName =>
        User?.FindFirstValue("storeName");

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;
}