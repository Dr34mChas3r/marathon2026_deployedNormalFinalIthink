using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Helpers;
using APIHandler.Repositories.Reservations;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Feedbacks;

public class GetFeedbackTokenHandler(IReservationRepository reservationRepository) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var claims = request.RequestContext?.Authorizer?.Claims;
        if (claims is null ||
            !claims.TryGetValue("sub", out string? userId) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "Authentication is required.", context.Logger);
        }

        if (!RoleHelper.IsWaiter(request))
        {
            return ResponseBuilder.CreateErrorResponse(403, "Only the assigned waiter can create this feedback QR code.", context.Logger);
        }

        if (request.QueryStringParameters is null ||
            !request.QueryStringParameters.TryGetValue("reservationId", out string? reservationId) ||
            string.IsNullOrWhiteSpace(reservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);
        }

        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId);
        if (reservation is null)
        {
            return ResponseBuilder.CreateErrorResponse(404, "Reservation was not found.", context.Logger);
        }

        if (reservation.WaiterId != userId)
        {
            return ResponseBuilder.CreateErrorResponse(403, "Only the assigned waiter can create this feedback QR code.", context.Logger);
        }

        string? token = await reservationRepository.GetOrCreateFeedbackTokenAsync(reservationId);
        if (token is null)
        {
            return ResponseBuilder.CreateErrorResponse(404, "Reservation was not found.", context.Logger);
        }

        return ResponseBuilder.CreateJsonResponse(200, token);
    }
}
