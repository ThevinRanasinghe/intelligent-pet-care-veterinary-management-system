namespace PetCare.Application.DTOs.Billing;

public record QuotationItemDto(
    Guid Id,
    string Category,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public record QuotationResponseDto(
    Guid Id,
    Guid AppointmentId,
    decimal Budget,
    decimal Subtotal,
    decimal Total,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<QuotationItemDto> Items
);

public record CreateQuotationItemRequestDto(
    string Category,
    string Description,
    int Quantity,
    decimal UnitPrice
);

public record CreateQuotationDto(
    Guid AppointmentId,
    decimal Budget,
    IReadOnlyList<CreateQuotationItemRequestDto> Items
);

public record UpdateQuotationDto(
    decimal Budget,
    IReadOnlyList<CreateQuotationItemRequestDto> Items
);
