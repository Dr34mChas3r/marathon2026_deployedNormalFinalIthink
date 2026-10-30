using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitGroupReservationHandler(IGroupReservationService groupReservationService) : IApiRequestHandler
{
    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (userId is null)
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Group reservation ID is required.", context.Logger);

        SubmitGroupReservationRequest? body;
        try
        {
            body = string.IsNullOrWhiteSpace(request.Body)
                ? null
                : JsonSerializer.Deserialize<SubmitGroupReservationRequest>(request.Body, RequestOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (string.IsNullOrWhiteSpace(body?.ReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);

        try
        {
            var items = await groupReservationService.SubmitGroupReservationAsync(userId, groupReservationId, body.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, items);
        }
        catch (GroupReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
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
