namespace TableCreater.WPF.Models;

// ============================================================================
// CUSTOMER DTOs / Records
// Mapped from Java CustomerCreateDto, CustomerReadDto, CustomerUpdateDto.
// ============================================================================

/// <summary>
/// Request model for creating a new customer.
/// Mapped from Java CustomerCreateDto.
/// </summary>
public record CustomerCreateRequest
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

/// <summary>
/// Request model for updating an existing customer.
/// Only non-null fields are applied (partial update pattern).
/// Mapped from Java CustomerUpdateDto.
/// </summary>
public record CustomerUpdateRequest
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
}

/// <summary>
/// Response model returned when reading a customer.
/// Mapped from Java CustomerReadDto.
/// </summary>
public record CustomerReadResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}
