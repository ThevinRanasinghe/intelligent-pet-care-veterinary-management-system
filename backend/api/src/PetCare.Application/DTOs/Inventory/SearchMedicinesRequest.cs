// SearchMedicinesRequest.cs
namespace PetCare.Application.DTOs.Inventory;

public class SearchMedicinesRequest
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public bool? LowStockOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}