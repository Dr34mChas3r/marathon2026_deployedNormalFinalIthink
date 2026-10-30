using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace APIHandler.Repositories.Reservations;

public class ReservationRepository : IReservationRepository
{
    private static readonly string ReservationsTableName = ResourceNames.ReservationsTableName;
    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonDynamoDB _dynamoDbClient;

    public ReservationRepository(IDynamoDBContext dynamoDbContext, IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
        _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
    }

    public async Task<Reservation?> GetReservationByIdAsync(string reservationId)
    {
        return await _dynamoDbContext.LoadAsync<Reservation>(reservationId, DynamoConfig.LoadConfig(ReservationsTableName));
    }

    public async Task<List<Reservation>> GetReservationsByTableIdAsync(string tableId)
    {
        var queryConfig = DynamoConfig.QueryConfig(ReservationsTableName);
        queryConfig.IndexName = ResourceNames.ReservationsTableGlobalIndex;
        queryConfig.ConsistentRead = false;

        return await _dynamoDbContext.QueryAsync<Reservation>(tableId, queryConfig).GetRemainingAsync();
    }

    public async Task<IEnumerable<Reservation>> GetReservationsByWaiterIdAsync(string waiterId)
    {
        var queryConfig = DynamoConfig.QueryConfig(ReservationsTableName);
        queryConfig.IndexName = ResourceNames.ReservationsWaiterIdGlobalIndex;
        queryConfig.ConsistentRead = false;

        return await _dynamoDbContext
            .QueryAsync<Reservation>(waiterId, queryConfig)
            .GetRemainingAsync();
    }

    public async Task<IEnumerable<Reservation>> GetReservationsByCustomerIdAsync(string userId)
    {
        var queryConfig = DynamoConfig.QueryConfig(ReservationsTableName);
        queryConfig.IndexName = ResourceNames.ReservationsCustomerIdGlobalIndex;
        queryConfig.ConsistentRead = false;

        return await _dynamoDbContext
            .QueryAsync<Reservation>(userId, queryConfig)
            .GetRemainingAsync();
    }

    public async Task SaveReservationAsync(Reservation reservation)
    {
        await _dynamoDbContext.SaveAsync(reservation, DynamoConfig.SaveConfig(ReservationsTableName));
    }

    public async Task<string?> GetOrCreateFeedbackTokenAsync(string reservationId)
    {
        string generatedToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        try
        {
            var response = await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = ReservationsTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["id"] = new() { S = reservationId }
                },
                UpdateExpression = "SET #feedbackToken = if_not_exists(#feedbackToken, :generatedToken)",
                ConditionExpression = "attribute_exists(#id)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#id"] = "id",
                    ["#feedbackToken"] = "feedbackToken"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":generatedToken"] = new() { S = generatedToken }
                },
                ReturnValues = ReturnValue.ALL_NEW
            });

            return response.Attributes["feedbackToken"].S;
        }
        catch (ConditionalCheckFailedException)
        {
            return null;
        }
    }
}