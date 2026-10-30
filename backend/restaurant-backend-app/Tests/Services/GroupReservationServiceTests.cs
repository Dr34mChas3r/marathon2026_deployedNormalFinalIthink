using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        reservation.GuestCount = 3;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_StillReturnsInviteDetails(
        GroupReservation groupReservation,
        Reservation reservation,
        Table table)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;
        reservation.GuestCount = 2;
        reservation.TableId = table.Id;
        reservation.CustomerId = null;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_SubmittedGroupStillReturnsInviteDetails(
        GroupReservation groupReservation,
        Reservation reservation,
        Table table)
    {
        groupReservation.Status = GroupReservationStatus.Submitted;
        groupReservation.ReservationId = reservation.Id;
        reservation.TableId = table.Id;
        reservation.CustomerId = null;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
    }

    [Fact]
    public async Task GetGroupReservationByTokenAsync_OwnerAndOneGuestCapacity_AllowsInviteForGuest()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id",
            GuestCount = 2
        };
        var table = new Table { Id = reservation.TableId, LocationId = "location-id", TableNumber = 1, Capacity = 2 };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByTokenAsync(group.ReservationToken))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _tableRepositoryMock
            .Setup(repository => repository.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        var result = await _service.GetGroupReservationByTokenAsync(group.ReservationToken);

        Assert.Equal(group.GroupReservationId, result.GroupReservationId);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_ConsolidatesSubmittedGuestItemsAndUpdatesStatuses()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        var order = new Order
        {
            OrderId = "order-id",
            CustomerId = "owner-id",
            ReservationId = reservation.Id,
            ReservationAddress = "Address",
            ReservationTime = "Time",
            Dishes = [new OrderItem { DishId = "existing-dish", Quantity = 4, State = OrderItemState.Confirmed }]
        };
        var sessions = new List<GuestSession>
        {
            new()
            {
                Id = "guest-1",
                GroupReservationId = group.GroupReservationId,
                GuestName = "Guest 1",
                Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }],
                IsSubmitted = true
            },
            new()
            {
                Id = "guest-2",
                GroupReservationId = group.GroupReservationId,
                GuestName = "Guest 2",
                Dishes =
                [
                    new OrderItem { DishId = "dish-1", Quantity = 1 },
                    new OrderItem { DishId = "dish-2", Quantity = 3 }
                ],
                IsSubmitted = true
            },
            new()
            {
                Id = "guest-3",
                GroupReservationId = group.GroupReservationId,
                GuestName = "Guest 3",
                Dishes = [new OrderItem { DishId = "dish-2", Quantity = 100 }],
                IsSubmitted = false
            }
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync(sessions);
        _orderRepositoryMock
            .Setup(repository => repository.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(order);

        var result = await _service.SubmitGroupReservationAsync("owner-id", group.GroupReservationId, reservation.Id);

        Assert.Equal(
            [new SubmittedGroupOrderItem("dish-1", 3), new SubmittedGroupOrderItem("dish-2", 3)],
            result);
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(["existing-dish", "dish-1", "dish-2"], order.Dishes.Select(item => item.DishId));
        Assert.Equal([4, 3, 3], order.Dishes.Select(item => item.Quantity));
        Assert.Equal(GroupReservationStatus.Submitted, group.Status);
        _orderRepositoryMock.Verify(repository => repository.SaveOrderAsync(order), Times.Once);
        _groupReservationRepositoryMock.Verify(repository => repository.SaveGroupReservationAsync(group), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_IncludesSubmittedOwnerSessionItemsInConsolidation()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        var ownerSession = new GuestSession
        {
            Id = "owner-session",
            GroupReservationId = group.GroupReservationId,
            GuestName = "Owner",
            IsOwnerSession = true,
            OwnerUserId = "owner-id",
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "caesar", Quantity = 1 }]
        };
        var guestSession = new GuestSession
        {
            Id = "guest-session",
            GroupReservationId = group.GroupReservationId,
            GuestName = "Guest",
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "caesar", Quantity = 2 }]
        };
        var order = new Order
        {
            OrderId = "order-id",
            CustomerId = "owner-id",
            ReservationId = reservation.Id,
            ReservationAddress = "Address",
            ReservationTime = "Time",
            Dishes = []
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([ownerSession, guestSession]);
        _orderRepositoryMock
            .Setup(repository => repository.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(order);

        var result = await _service.SubmitGroupReservationAsync("owner-id", group.GroupReservationId, reservation.Id);

        var item = Assert.Single(result);
        Assert.Equal("caesar", item.DishId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(3, Assert.Single(order.Dishes).Quantity);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NoSubmittedGuestSessions_ThrowsValidationException()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token"
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync("owner-id", group.GroupReservationId, reservation.Id));

        _orderRepositoryMock.Verify(repository => repository.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_SubmittedSessionsWithoutDishes_ThrowsValidationException()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token"
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        var session = new GuestSession
        {
            Id = "guest-id",
            GroupReservationId = group.GroupReservationId,
            GuestName = "Guest",
            Dishes = [],
            IsSubmitted = true
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([session]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync("owner-id", group.GroupReservationId, reservation.Id));

        _orderRepositoryMock.Verify(repository => repository.GetOrderByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_MissingOrder_ThrowsValidationException()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token"
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        var session = new GuestSession
        {
            Id = "guest-id",
            GroupReservationId = group.GroupReservationId,
            GuestName = "Guest",
            Dishes = [new OrderItem { DishId = "dish-id", Quantity = 1 }],
            IsSubmitted = true
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([session]);
        _orderRepositoryMock
            .Setup(repository => repository.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync("owner-id", group.GroupReservationId, reservation.Id));

        _groupReservationRepositoryMock.Verify(repository => repository.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_MissingGroup_ThrowsNotFound()
    {
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync("missing-group"))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.SubmitGroupReservationAsync("owner-id", "missing-group", "reservation-id"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_MissingReservation_ThrowsNotFound()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "missing-reservation",
            ReservationToken = "token"
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(group.ReservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.SubmitGroupReservationAsync("owner-id", group.GroupReservationId, group.ReservationId));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_RejectsNonOwner()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token"
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId,
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.SubmitGroupReservationAsync("someone-else", group.GroupReservationId, reservation.Id));

        _guestSessionRepositoryMock.Verify(
            repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateGroupReservationAsync_CreatesOwnerSessionForReservationCustomer()
    {
        var reservation = new Reservation
        {
            Id = "reservation-id",
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        GuestSession? savedSession = null;
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _guestSessionRepositoryMock
            .Setup(repository => repository.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(session => savedSession = session);

        var group = await _service.CreateGroupReservationAsync(reservation.Id);

        Assert.NotNull(savedSession);
        Assert.Equal(group.GroupReservationId, savedSession.GroupReservationId);
        Assert.Equal(reservation.CustomerId, savedSession.OwnerUserId);
        Assert.True(savedSession.IsOwnerSession);
    }
}
