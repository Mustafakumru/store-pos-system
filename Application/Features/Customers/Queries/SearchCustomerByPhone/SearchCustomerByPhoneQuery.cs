using MediatR;

namespace Application.Features.Customers.Queries.SearchCustomerByPhone;

/// <summary>
/// Fiş ekranı "Müşteri Ara" / iade ekranı "Müşteri Bilgisiyle İade"
/// için telefon numarasıyla müşteri sorgular.
/// </summary>
public record SearchCustomerByPhoneQuery(string PhoneNumber)
    : IRequest<CustomerDto?>;

public record CustomerDto(
    int Id,
    string PhoneNumber,
    string FullName,
    string? Email,
    bool SmsConsent);