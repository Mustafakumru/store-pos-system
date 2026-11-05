using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customers.Queries.SearchCustomerByPhone;

public class SearchCustomerByPhoneQueryHandler
    : IRequestHandler<SearchCustomerByPhoneQuery, CustomerDto?>
{
    private readonly IApplicationDbContext _context;

    public SearchCustomerByPhoneQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CustomerDto?> Handle(
        SearchCustomerByPhoneQuery request,
        CancellationToken cancellationToken)
    {
        // Yeni kayıtlar normalize (10 hane) saklanır; eski kayıtlar girildiği gibi.
        var raw = (request.PhoneNumber ?? string.Empty).Trim();
        var normalized = Application.Common.Validation.CommonRules.NormalizePhone(raw);

        return await _context.Customers
            .Where(c => (c.PhoneNumber == normalized || c.PhoneNumber == raw) && c.IsActive)
            .Select(c => new CustomerDto(
                c.Id,
                c.PhoneNumber,
                c.FullName,
                c.Email,
                c.SmsConsent))
            .FirstOrDefaultAsync(cancellationToken);
    }
}