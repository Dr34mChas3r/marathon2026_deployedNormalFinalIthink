namespace APIHandler.Models.GroupReservation;

public record SubmitGroupReservationRequest(string ReservationId);

public record SubmittedGroupOrderItem(string DishId, int Quantity);
