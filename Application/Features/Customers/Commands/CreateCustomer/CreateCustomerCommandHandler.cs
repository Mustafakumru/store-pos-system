using Application.Common.Interfaces;
using Application.Features.Customers.Queries.SearchCustomerByPhone;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customers.Commands.CreateCustomer;

public class CreateCustomerCommandHandler
    : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCustomerCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CustomerDto> Handle(
        CreateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        // Telefon tek biçimde (10 hane) saklanır - farklı yazımlar aynı müşteriyi bulur
        var phone = Application.Common.Validation.CommonRules.NormalizePhone(request.PhoneNumber);

        var exists = await _context.Customers
            .AnyAsync(c => c.PhoneNumber == phone, cancellationToken);

        if (exists)
            throw new InvalidOperationException(
                "Bu telefon numarasıyla kayıtlı bir müşteri zaten var.");

        var customer = new Customer
        {
            PhoneNumber = phone,
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            SmsConsent = request.SmsConsent,
            IsActive = true
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return new CustomerDto(
            customer.Id,
            customer.PhoneNumber,
            customer.FullName,
            customer.Email,
            customer.SmsConsent);
    }
}