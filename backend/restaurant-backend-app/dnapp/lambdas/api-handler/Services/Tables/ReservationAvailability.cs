using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Models.TimeSlots;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APIHandler.Services.Tables;

public static class ReservationAvailability
{
    public static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(90);
    public static readonly TimeSpan ClearingBuffer = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SlotInterval = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan OpeningTime = TimeSpan.FromHours(10);
    public static readonly TimeSpan LastStartTime = TimeSpan.FromHours(21.5);
    public static readonly TimeSpan ClosingTime = LastStartTime + ReservationDuration + ClearingBuffer;

    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Tbilisi");

    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime GetBusinessDate(DateTime utcTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcTime), BusinessTimeZone).Date;

    public static DateTime ToUtc(DateTime businessLocalTime) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(businessLocalTime, DateTimeKind.Unspecified),
            BusinessTimeZone);

    public static DateTime GetClosingTimeUtc(DateTime utcTime) =>
        ToUtc(GetBusinessDate(utcTime) + ClosingTime);

    public static bool HasAvailableWindow(
        DateTime startTimeUtc,
        DateTime closingTimeUtc,
        IEnumerable<Reservation> reservations)
    {
        var current = AsUtc(startTimeUtc);
        var closingTime = AsUtc(closingTimeUtc);

        foreach (var reservation in reservations
                     .Where(IsBlocking)
                     .OrderBy(reservation => AsUtc(reservation.StartTime)))
        {
            var reservationStart = AsUtc(reservation.StartTime);
            var reservationEndWithBuffer = AsUtc(reservation.EndTime) + ClearingBuffer;

            if (reservationEndWithBuffer <= current)
                continue;

            if (reservationStart - current >= ReservationDuration + ClearingBuffer)
                return true;

            if (reservationEndWithBuffer > current)
                current = reservationEndWithBuffer;
        }

        return closingTime - current >= ReservationDuration + ClearingBuffer;
    }

    public static IReadOnlyList<TimeSlotResponse> GetAvailableSlots(
        DateTime requestedStartTimeUtc,
        DateTime nowUtc,
        IEnumerable<Reservation> reservations)
    {
        var businessDate = GetBusinessDate(requestedStartTimeUtc);
        var earliestStart = AsUtc(nowUtc) + ClearingBuffer;
        var blockingReservations = reservations.Where(IsBlocking).ToList();
        var slots = new List<TimeSlotResponse>();

        for (var start = businessDate + OpeningTime; start.TimeOfDay <= LastStartTime; start = start.Add(SlotInterval))
        {
            var slotStart = ToUtc(start);
            if (slotStart < earliestStart)
                continue;

            var slotEnd = slotStart + ReservationDuration;
            var conflicts = blockingReservations.Any(reservation =>
            {
                var reservedStart = AsUtc(reservation.StartTime);
                var reservedEnd = AsUtc(reservation.EndTime);

                return slotStart < reservedEnd + ClearingBuffer &&
                    slotEnd > reservedStart - ClearingBuffer;
            });

            if (!conflicts)
            {
                slots.Add(new TimeSlotResponse
                {
                    Start = slotStart,
                    End = slotEnd,
                });
            }
        }

        return slots;
    }

    private static bool IsBlocking(Reservation reservation) =>
        reservation.Status is ReservationStatus.Reserved or ReservationStatus.InProgress;
}
