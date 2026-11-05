using Application.Features.Customers.Queries.SearchCustomerByPhone;
using MediatR;

namespace Application.Features.Customers.Commands.CreateCustomer;

/// <summary>
/// Fiş ekranı "Yeni Müşteri" kaydı.
/// Telefon numarası benzersiz olmalıdır.
/// </summary>
public record CreateCustomerCommand(
    string PhoneNumber,
    string FullName,
    string? Email,
    bool SmsConsent) : IRequest<CustomerDto>;