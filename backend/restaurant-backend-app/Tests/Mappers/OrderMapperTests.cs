using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Mappers;

namespace APIHandler.Tests.Mappers;

public class OrderMapperTests
{
    [Fact]
    public void ToOrderResponse_PreservesOrderItemState()
    {
        var order = new Order
        {
            OrderId = "order-id",
            CustomerId = "customer-id",
            ReservationId = "reservation-id",
            ReservationAddress = "Restaurant",
            ReservationTime = "Oct 30, 2026",
            Dishes =
            [
                new OrderItem
                {
                    DishId = "dish-id",
                    Quantity = 2,
                    State = OrderItemState.Confirmed,
                }
            ],
        };
        var dish = new Dish
        {
            Id = "dish-id",
            Name = "Soup",
            Description = "",
            Weight = "",
            Calories = "",
            Proteins = "",
            Fats = "",
            Carbohydrates = "",
            Vitamins = "",
            LocationId = "location-id",
            DishType = DishType.MainCourse,
            State = DishState.Available,
        };

        var response = OrderMapper.ToOrderResponse(order, [dish]);

        Assert.Equal(OrderItemState.Confirmed, response.OrderItems[0].State);
    }
}
