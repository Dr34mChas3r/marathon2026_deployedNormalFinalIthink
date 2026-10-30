using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.Feedbacks;
using APIHandler.Entities;
using APIHandler.Models.Feedbacks;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Auth;
using APIHandler.Services.Feedbacks;
using Moq;
using System.Collections.Generic;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class FeedbackTokenHandlerTests
{
    [Fact]
    public async Task HandleRequest_ReturnsReservationFeedbackToken()
    {
        var reservationRepository = new Mock<IReservationRepository>();
        reservationRepository
            .Setup(repository => repository.GetReservationByIdAsync("reservation-id"))
            .ReturnsAsync(new Reservation
            {
                Id = "reservation-id",
                TableId = "table-id",
                WaiterId = "waiter-id",
                GuestCount = 1
            });
        reservationRepository
            .Setup(repository => repository.GetOrCreateFeedbackTokenAsync("reservation-id"))
            .ReturnsAsync("opaque-token");
        var handler = new GetFeedbackTokenHandler(reservationRepository.Object);

        var response = await handler.HandleRequest(new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>
            {
                ["reservationId"] = "reservation-id"
            },
            RequestContext = CreateWaiterRequestContext()
        }, CreateLambdaContext());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("opaque-token", JsonSerializer.Deserialize<string>(response.Body));
    }

    [Fact]
    public async Task HandleRequest_ReturnsNotFoundForUnknownReservation()
    {
        var reservationRepository = new Mock<IReservationRepository>();
        reservationRepository
            .Setup(repository => repository.GetReservationByIdAsync("missing-id"))
            .ReturnsAsync((Reservation?)null);
        var handler = new GetFeedbackTokenHandler(reservationRepository.Object);

        var response = await handler.HandleRequest(new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>
            {
                ["reservationId"] = "missing-id"
            },
            RequestContext = CreateWaiterRequestContext()
        }, CreateLambdaContext());

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task CreateFeedback_RejectsInvalidAnonymousToken()
    {
        var reservationRepository = new Mock<IReservationRepository>();
        reservationRepository
            .Setup(repository => repository.GetReservationByIdAsync("reservation-id"))
            .ReturnsAsync(CreateReservation("correct-token"));
        var feedbackService = new Mock<IFeedbackService>();
        var handler = new CreateFeedbackHandler(
            feedbackService.Object,
            Mock.Of<IUserService>(),
            reservationRepository.Object);

        var response = await handler.HandleRequest(new APIGatewayProxyRequest
        {
            Body = JsonSerializer.Serialize(new CreateFeedbackRequest
            {
                ReservationId = "reservation-id",
                Token = "wrong-token",
                Type = FeedbackType.Service
            })
        }, CreateLambdaContext());

        Assert.Equal(403, response.StatusCode);
        feedbackService.Verify(service => service.CreateFeedbackAsync(It.IsAny<CreateFeedbackRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateFeedback_AcceptsValidAnonymousToken()
    {
        var reservationRepository = new Mock<IReservationRepository>();
        reservationRepository
            .Setup(repository => repository.GetReservationByIdAsync("reservation-id"))
            .ReturnsAsync(CreateReservation("correct-token"));
        var feedbackService = new Mock<IFeedbackService>();
        feedbackService
            .Setup(service => service.GetFeedbackForReservationAsync("reservation-id"))
            .ReturnsAsync([]);
        feedbackService
            .Setup(service => service.CreateFeedbackAsync(It.IsAny<CreateFeedbackRequest>()))
            .ReturnsAsync(new FeedbackResponseItem
            {
                ReservationId = "reservation-id",
                LocationId = "location-id"
            });
        var handler = new CreateFeedbackHandler(
            feedbackService.Object,
            Mock.Of<IUserService>(),
            reservationRepository.Object);

        var response = await handler.HandleRequest(new APIGatewayProxyRequest
        {
            Body = JsonSerializer.Serialize(new CreateFeedbackRequest
            {
                ReservationId = "reservation-id",
                Token = "correct-token",
                Type = FeedbackType.Service
            })
        }, CreateLambdaContext());

        Assert.Equal(200, response.StatusCode);
        feedbackService.Verify(service => service.CreateFeedbackAsync(It.IsAny<CreateFeedbackRequest>()), Times.Once);
    }

    private static Reservation CreateReservation(string feedbackToken) => new()
    {
        Id = "reservation-id",
        TableId = "table-id",
        WaiterId = "waiter-id",
        GuestCount = 1,
        FeedbackToken = feedbackToken
    };

    private static ILambdaContext CreateLambdaContext()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(lambdaContext => lambdaContext.Logger).Returns(Mock.Of<ILambdaLogger>());
        return context.Object;
    }

    private static APIGatewayProxyRequest.ProxyRequestContext CreateWaiterRequestContext() => new()
    {
        Authorizer = new APIGatewayCustomAuthorizerContext
        {
            Claims = new Dictionary<string, string>
            {
                ["sub"] = "waiter-id",
                ["cognito:groups"] = "[\"Waiter\"]"
            }
        }
    };
}
