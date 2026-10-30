using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.Orders;
using APIHandler.Services.Orders;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Order;

public class UpdateKitchenOrderStatusHandler(IOrderService orderService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? waiterId = UserContextHelper.GetAuthenticatedUserId(request);
        if (waiterId is null)
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        if (!UserContextHelper.IsRequestFromWaiter(request))
            return ResponseBuilder.CreateErrorResponse(403, "Only waiters can update kitchen order status.", context.Logger);

        if (request.PathParameters?.TryGetValue("reservationId", out string? reservationId) != true ||
            string.IsNullOrWhiteSpace(reservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);

        UpdateKitchenOrderStatusRequest? updateRequest;
        if (string.IsNullOrWhiteSpace(request.Body))
            return ResponseBuilder.CreateErrorResponse(400, "A valid kitchen order status is required.", context.Logger);

        try
        {
            updateRequest = JsonSerializer.Deserialize<UpdateKitchenOrderStatusRequest>(request.Body);
        }
        catch (JsonException)
        {
            updateRequest = null;
        }

        if (updateRequest is null)
            return ResponseBuilder.CreateErrorResponse(400, "A valid kitchen order status is required.", context.Logger);

        try
        {
            var order = await orderService.UpdateKitchenStatusAsync(waiterId, reservationId, updateRequest.Status);
            return ResponseBuilder.CreateSuccessResponse(new { status = order.Status });
        }
        catch (ReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (ActionForbiddenException exception)
        {
            return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger);
        }
        catch (ValidationException exception)
        {
            return ResponseBuilder.CreateErrorResponse(400, exception.Message, context.Logger);
        }
    }
}
