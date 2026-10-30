using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Handlers.Order;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.Reports;
using APIHandler.Models.Users;
using APIHandler.Services.Dishes;
using APIHandler.Services.Feedbacks;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reports.Interfaces;
using APIHandler.Services.Reservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class WaiterOrderAndReservationStatusTests
{
    [Fact]
    public async Task CreateOrderHandler_UsesWaiterIdForWaiterCreatedReservation()
    {
        const string waiterId = "waiter-id";
        const string reservationId = "reservation-id";
        const string dishId = "dish-id";
        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-id",
            CustomerId = null,
            WaiterId = waiterId,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            GuestCount = 2,
            Status = ReservationStatus.InProgress,
        };
        var dish = new Dish
        {
            Id = dishId,
            Name = "Dish",
            Description = "",
            Weight = "",
            Calories = "",
            Proteins = "",
            Fats = "",
            Carbohydrates = "",
            Vitamins = "",
            LocationId = "location-id",
            State = DishState.Available,
        };
        Order? savedOrder = null;
        var dishService = new Mock<IDishService>();
        dishService.Setup(service => service.GetDishByIdAsync(dishId)).ReturnsAsync(dish);
        var reservationService = new Mock<IReservationService>();
        reservationService.Setup(service => service.GetReservationByIdAsync(reservationId)).ReturnsAsync(reservation);
        var locationService = new Mock<ILocationService>();
        locationService.Setup(service => service.GetLocationByTableIdAsync("table-id"))
            .ReturnsAsync(new Location
            {
                Id = "location-id",
                Address = "Restaurant address",
                Description = "",
                AverageOccupancy = "",
            });
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.AddOrderForWaiterAsync(It.IsAny<Order>()))
            .Callback<Order>(order => savedOrder = order)
            .Returns(Task.CompletedTask);
        var handler = new CreateOrderHandler(
            dishService.Object,
            reservationService.Object,
            locationService.Object,
            orderService.Object);

        var response = await handler.HandleRequest(
            CreateWaiterRequest(
                JsonSerializer.Serialize(new
                {
                    reservationId,
                    orderItems = new[] { new { dishId, quantity = 1 } },
                })),
            CreateLambdaContext());

        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(savedOrder);
        Assert.Equal(waiterId, savedOrder.CustomerId);
    }

    [Fact]
    public async Task UpdateReservationStatusHandler_AllowsCompletionWhenOrderExistsWithoutFeedback()
    {
        const string reservationId = "reservation-id";
        var reservation = CreateInProgressReservation(reservationId);
        var reservationService = new Mock<IReservationService>();
        reservationService.Setup(service => service.GetReservationByIdAsync(reservationId)).ReturnsAsync(reservation);
        reservationService.Setup(service => service.UpdateReservationAsync(reservation)).ReturnsAsync(true);
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(new Order
            {
                OrderId = "order-id",
                CustomerId = "waiter-id",
                ReservationId = reservationId,
                ReservationAddress = "",
                ReservationTime = "",
                Dishes = [new OrderItem { DishId = "dish-id", Quantity = 1 }],
            });
        var feedbackService = new Mock<IFeedbackService>();
        feedbackService.Setup(service => service.GetFeedbackForReservationAsync(reservationId))
            .ReturnsAsync([]);
        var reportService = new Mock<IReportService>();
        var report = new Report
        {
            ReservationId = reservationId,
            WaiterId = "waiter-id",
            LocationId = "location-id",
        };
        reportService.Setup(service => service.GenerateReportAsync(reservationId)).ReturnsAsync(report);
        reportService.Setup(service => service.SendReportToSqsAsync(report)).Returns(Task.CompletedTask);
        var handler = new UpdateReservationStatusHandler(
            reservationService.Object,
            orderService.Object,
            feedbackService.Object,
            reportService.Object);

        var response = await handler.HandleRequest(
            CreateWaiterRequest("{\"newStatus\":3}", reservationId),
            CreateLambdaContext());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(ReservationStatus.Completed, reservation.Status);
    }

    [Fact]
    public async Task UpdateReservationStatusHandler_RejectsCompletionWithoutOrderOrFeedback()
    {
        const string reservationId = "reservation-id";
        var reservation = CreateInProgressReservation(reservationId);
        var reservationService = new Mock<IReservationService>();
        reservationService.Setup(service => service.GetReservationByIdAsync(reservationId)).ReturnsAsync(reservation);
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByReservationIdAsync(reservationId)).ReturnsAsync((Order?)null);
        var feedbackService = new Mock<IFeedbackService>();
        feedbackService.Setup(service => service.GetFeedbackForReservationAsync(reservationId))
            .ReturnsAsync([]);
        var handler = new UpdateReservationStatusHandler(
            reservationService.Object,
            orderService.Object,
            feedbackService.Object,
            new Mock<IReportService>().Object);

        var response = await handler.HandleRequest(
            CreateWaiterRequest("{\"newStatus\":3}", reservationId),
            CreateLambdaContext());

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(ReservationStatus.InProgress, reservation.Status);
        reservationService.Verify(service => service.UpdateReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    private static Reservation CreateInProgressReservation(string reservationId) => new()
    {
        Id = reservationId,
        TableId = "table-id",
        WaiterId = "waiter-id",
        StartTime = DateTime.UtcNow.AddHours(-1),
        EndTime = DateTime.UtcNow.AddHours(1),
        GuestCount = 2,
        Status = ReservationStatus.InProgress,
    };

    private static APIGatewayProxyRequest CreateWaiterRequest(string body, string? reservationId = null) => new()
    {
        Body = body,
        PathParameters = reservationId is null ? null : new Dictionary<string, string> { ["reservationId"] = reservationId },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string>
                {
                    ["sub"] = "waiter-id",
                    ["cognito:groups"] = Roles.Waiter.ToString(),
                },
            },
        },
    };

    private static ILambdaContext CreateLambdaContext()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(lambdaContext => lambdaContext.Logger).Returns(Mock.Of<ILambdaLogger>());
        return context.Object;
    }
}
