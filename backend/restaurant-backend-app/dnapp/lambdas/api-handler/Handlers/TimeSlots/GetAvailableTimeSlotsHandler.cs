using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Services.Tables;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.TimeSlots;

public class GetAvailableTimeSlotsHandler : IApiRequestHandler
{
    private static readonly string ReservationsTableName = ResourceNames.ReservationsTableName;
    private static readonly string ReservationsTableGlobalIndex = ResourceNames.ReservationsTableGlobalIndex;

    private readonly IDynamoDBContext _dbContext;

    public GetAvailableTimeSlotsHandler(IDynamoDBContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // /tables/{id}/available-time-slots?date
        var pathParams = request.PathParameters;
        var queryParams = request.QueryStringParameters ?? new Dictionary<string, string>();

        if (pathParams == null ||
            !pathParams.TryGetValue("id", out var tableId) ||
            string.IsNullOrWhiteSpace(tableId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "No table id was found in request.", context.Logger);
        }

        if (!queryParams.TryGetValue("startTime", out var startTimeStr) || !DateTime.TryParse(startTimeStr, out var startTime))
        {
            return ResponseBuilder.CreateErrorResponse(
                400,
                "Missing or invalid 'startTime' parameter. Format: ISO 8601 (e.g., 2023-10-01T00:00:00Z).",
                context.Logger);
        }

        var queryConfig = DynamoConfig.QueryConfig(ReservationsTableName);
        queryConfig.IndexName = ReservationsTableGlobalIndex;
        queryConfig.ConsistentRead = false;
        var reservations = await _dbContext
            .QueryAsync<Reservation>(tableId, queryConfig)
            .GetRemainingAsync();

        context.Logger.Log($"Table id: {tableId}");
        context.Logger.Log($"Reservations: {JsonSerializer.Serialize(reservations)}");

        var slots = ReservationAvailability.GetAvailableSlots(startTime, DateTime.UtcNow, reservations);

        return ResponseBuilder.CreateSuccessResponse(slots, 200);
    }
}