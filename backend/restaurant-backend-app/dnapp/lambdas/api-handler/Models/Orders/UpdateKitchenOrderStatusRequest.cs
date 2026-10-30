using APIHandler.Entities;
using System.Text.Json.Serialization;

namespace APIHandler.Models.Orders;

public record UpdateKitchenOrderStatusRequest(
    [property: JsonPropertyName("status")] OrderStatus Status);
