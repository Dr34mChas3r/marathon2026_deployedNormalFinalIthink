using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.Tables;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.Tables;

public class TableService : ITableService
{
    private readonly ITableRepository _tableRepository;
    private readonly IReservationRepository _reservationRepository;

    public TableService(ITableRepository tableRepository, IReservationRepository reservationRepository)
    {
        _tableRepository = tableRepository ?? throw new ArgumentNullException(nameof(tableRepository));
        _reservationRepository = reservationRepository ?? throw new ArgumentNullException(nameof(reservationRepository));
    }

    public async Task<Table> CreateTableAsync(CreateTableRequest request)
    {
        var table = new Table
        {
            Id = Guid.NewGuid().ToString(),
            LocationId = request.LocationId,
            Capacity = request.Capacity,
            TableNumber = request.TableNumber
        };

        await _tableRepository.SaveTableAsync(table);
        return table;
    }

    public async Task<IEnumerable<Table>> GetAvailableTablesAsync(string? locationId, int capacity, DateTime startTime)
    {
        var workingDayEnd = ReservationAvailability.GetClosingTimeUtc(startTime);

        IEnumerable<Table> tables;
        if (!string.IsNullOrEmpty(locationId))
        {
            tables = await _tableRepository.GetTablesByLocationIdAsync(locationId);
        }
        else
        {
            tables = await _tableRepository.GetAllTablesAsync();
        }

        if (capacity > 0)
        {
            tables = tables.Where(t => t.Capacity >= capacity);
        }

        var availableTables = new List<Table>();

        var checkTasks = tables.Select(async table =>
        {
            var isAvailable = await IsTableAvailableAsync(table.Id, startTime, workingDayEnd);
            if (isAvailable)
                availableTables.Add(table);
        });

        await Task.WhenAll(checkTasks);
        return availableTables;
    }

    public async Task<IEnumerable<Table>> GetAllTablesForLocationAsync(string locationId)
    {
        return await _tableRepository.GetTablesByLocationIdAsync(locationId);
    }

    public async Task<Table> UpdateTableAsync(string tableId, UpdateTableRequest request)
    {
        var table = await _tableRepository.GetTableByIdAsync(tableId)
            ?? throw new ValidationException($"Table '{tableId}' not found.");

        table.TableNumber = request.TableNumber;
        table.Capacity = request.Capacity;

        await _tableRepository.SaveTableAsync(table);
        return table;
    }

    public async Task DeleteTableAsync(string tableId)
    {
        var table = await _tableRepository.GetTableByIdAsync(tableId)
            ?? throw new ValidationException($"Table '{tableId}' not found.");

        // Check for active reservations before deleting
        var reservations = await _reservationRepository.GetReservationsByTableIdAsync(tableId);
        var activeReservations = reservations.Where(r =>
            r.Status == ReservationStatus.Reserved && r.EndTime > DateTime.UtcNow).ToList();

        if (activeReservations.Count > 0)
        {
            throw new ValidationException(
                $"Cannot delete table '{tableId}' because it has {activeReservations.Count} active upcoming reservations.");
        }

        await _tableRepository.DeleteTableAsync(table);
    }

    private async Task<bool> IsTableAvailableAsync(string tableId, DateTime startTime, DateTime workingDayEnd)
    {
        var reservations = await _reservationRepository.GetReservationsByTableIdAsync(tableId);

        return ReservationAvailability.HasAvailableWindow(startTime, workingDayEnd, reservations);
    }
}