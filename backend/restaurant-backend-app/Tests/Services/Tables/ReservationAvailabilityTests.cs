using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Services.Tables;

namespace APIHandler.Tests.Services.Tables;

public class ReservationAvailabilityTests
{
    private static readonly DateTime BusinessDate = new(2026, 11, 2);
    private static readonly DateTime NowUtc = new(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetAvailableSlots_OffersHalfHourStartsFromOpeningThroughLastStart()
    {
        var slots = ReservationAvailability.GetAvailableSlots(
            ReservationAvailability.ToUtc(BusinessDate.AddHours(10)),
            NowUtc,
            []);

        Assert.Equal(24, slots.Count);
        Assert.Equal(
            ReservationAvailability.ToUtc(BusinessDate.AddHours(10)),
            slots[0].Start);
        Assert.Equal(
            ReservationAvailability.ToUtc(BusinessDate.AddHours(21.5)),
            slots[^1].Start);
        Assert.Equal(slots[0].Start.AddMinutes(90), slots[0].End);
    }

    [Fact]
    public void GetAvailableSlots_EnforcesFifteenMinuteClearanceOnBothSides()
    {
        var reservation = BuildReservation(
            BusinessDate.AddHours(12).AddMinutes(40),
            BusinessDate.AddHours(14).AddMinutes(10));

        var slots = ReservationAvailability.GetAvailableSlots(
            ReservationAvailability.ToUtc(BusinessDate.AddHours(10)),
            NowUtc,
            [reservation]);

        Assert.DoesNotContain(
            slots,
            slot => slot.Start == ReservationAvailability.ToUtc(BusinessDate.AddHours(11)));
        Assert.Contains(
            slots,
            slot => slot.Start == ReservationAvailability.ToUtc(BusinessDate.AddHours(10.5)));
    }

    [Fact]
    public void GetAvailableSlots_OffersFirstGridStartAfterReservationAndClearingBuffer()
    {
        var reservation = BuildReservation(
            BusinessDate.AddHours(10),
            BusinessDate.AddHours(11.5));

        var slots = ReservationAvailability.GetAvailableSlots(
            ReservationAvailability.ToUtc(BusinessDate.AddHours(10)),
            NowUtc,
            [reservation]);

        Assert.DoesNotContain(
            slots,
            slot => slot.Start == ReservationAvailability.ToUtc(BusinessDate.AddHours(11.5)));
        Assert.Contains(
            slots,
            slot => slot.Start == ReservationAvailability.ToUtc(BusinessDate.AddHours(12)));
    }

    [Fact]
    public void HasAvailableWindow_RequiresReservationAndClearingTimeBeforeClosing()
    {
        var latestStart = ReservationAvailability.ToUtc(BusinessDate.AddHours(21.5));
        var closingTime = ReservationAvailability.GetClosingTimeUtc(latestStart);

        Assert.True(ReservationAvailability.HasAvailableWindow(latestStart, closingTime, []));
        Assert.False(
            ReservationAvailability.HasAvailableWindow(
                latestStart.AddMinutes(30),
                closingTime,
                []));
    }

    [Fact]
    public void GetAvailableSlots_IgnoresCancelledReservations()
    {
        var cancelledReservation = BuildReservation(
            BusinessDate.AddHours(10),
            BusinessDate.AddHours(11.5),
            ReservationStatus.Cancelled);

        var slots = ReservationAvailability.GetAvailableSlots(
            ReservationAvailability.ToUtc(BusinessDate.AddHours(10)),
            NowUtc,
            [cancelledReservation]);

        Assert.Contains(
            slots,
            slot => slot.Start == ReservationAvailability.ToUtc(BusinessDate.AddHours(10)));
    }

    private static Reservation BuildReservation(
        DateTime localStart,
        DateTime localEnd,
        ReservationStatus status = ReservationStatus.Reserved) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            TableId = "table-1",
            WaiterId = "waiter-1",
            StartTime = ReservationAvailability.ToUtc(localStart),
            EndTime = ReservationAvailability.ToUtc(localEnd),
            Status = status,
        };
}
