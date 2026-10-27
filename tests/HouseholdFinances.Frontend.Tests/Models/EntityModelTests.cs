using System.Text.Json;
using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Tests.Models;

/// <summary>
/// Construction tests for the client-side entity models. Assigning the representative values also
/// verifies the field names and semantic types at compile time: money fields accept a
/// <see cref="double"/>, identifiers accept a <see cref="Guid"/>, and the recurring entities accept
/// an <see cref="Interval"/>.
/// </summary>
public class EntityModelTests
{
    [Fact]
    public void User_CanBeConstructedWithRepresentativeValues()
    {
        var id = Guid.NewGuid();

        var user = new User
        {
            Id = id,
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Password = null
        };

        Assert.Equal(id, user.Id);
        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("ada@example.com", user.Email);
        Assert.Null(user.Password);
    }

    [Fact]
    public void Household_CanBeConstructedWithMoneyTotalsAndEntryCounts()
    {
        var id = Guid.NewGuid();

        var household = new Household
        {
            Id = id,
            Name = "Brown Household",
            Incomes = 4200.50,
            Payments = 2875.25,
            IncomeEntryCount = 3,
            BillEntryCount = 4
        };

        Assert.Equal(id, household.Id);
        Assert.Equal("Brown Household", household.Name);
        Assert.Equal(4200.50, household.Incomes);
        Assert.Equal(2875.25, household.Payments);
        Assert.Equal(3, household.IncomeEntryCount);
        Assert.Equal(4, household.BillEntryCount);
    }

    [Fact]
    public void Income_CanBeConstructedWithRecurrenceFieldsAndOwner()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 12, 31);

        var income = new Income
        {
            Id = id,
            Name = "Salary",
            Value = 1500.00,
            StartDate = start,
            EndDate = end,
            Recurring = true,
            Interval = Interval.Monthly,
            UserId = userId
        };

        Assert.Equal(id, income.Id);
        Assert.Equal("Salary", income.Name);
        Assert.Equal(1500.00, income.Value);
        Assert.Equal(start, income.StartDate);
        Assert.Equal(end, income.EndDate);
        Assert.True(income.Recurring);
        Assert.Equal(Interval.Monthly, income.Interval);
        Assert.Equal(userId, income.UserId);
    }

    [Fact]
    public void Bill_CanBeConstructedWithHouseholdAndUser()
    {
        var id = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 2, 1);
        var end = new DateTime(2026, 2, 1);

        var bill = new Bill
        {
            Id = id,
            Name = "Rent",
            Value = 1200.00,
            StartDate = start,
            EndDate = end,
            Recurring = false,
            Interval = Interval.Monthly,
            HouseholdId = householdId,
            UserId = userId
        };

        Assert.Equal(id, bill.Id);
        Assert.Equal("Rent", bill.Name);
        Assert.Equal(1200.00, bill.Value);
        Assert.Equal(start, bill.StartDate);
        Assert.Equal(end, bill.EndDate);
        Assert.False(bill.Recurring);
        Assert.Equal(Interval.Monthly, bill.Interval);
        Assert.Equal(householdId, bill.HouseholdId);
        Assert.Equal(userId, bill.UserId);
    }

    [Fact]
    public void Income_DeserializesNullIntervalAsNull()
    {
        const string json = """
            {
              "id": "0f6d5c4b-3a29-4e18-9c7d-6b5a4f3e2d1c",
              "name": "Bonus",
              "value": 500,
              "startDate": "2026-03-01T00:00:00Z",
              "endDate": "2026-03-01T00:00:00Z",
              "recurring": false,
              "interval": null,
              "userId": "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e"
            }
            """;

        var income = JsonSerializer.Deserialize<Income>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(income);
        Assert.False(income!.Recurring);
        Assert.Null(income.Interval);
    }

    [Fact]
    public void Bill_DeserializesNullIntervalAsNull()
    {
        const string json = """
            {
              "id": "5d4c3b2a-1f0e-4d9c-8b7a-6f5e4d3c2b1a",
              "name": "Roof repair",
              "value": 900,
              "startDate": "2026-04-15T00:00:00Z",
              "endDate": "2026-04-15T00:00:00Z",
              "recurring": false,
              "interval": null,
              "householdId": "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
              "userId": "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e"
            }
            """;

        var bill = JsonSerializer.Deserialize<Bill>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(bill);
        Assert.False(bill!.Recurring);
        Assert.Null(bill.Interval);
    }

    [Fact]
    public void Item_DescriptionMayBeNull()
    {
        var id = Guid.NewGuid();
        var householdId = Guid.NewGuid();

        var item = new Item
        {
            Id = id,
            Name = "Groceries",
            Description = null,
            Price = 84.13,
            HouseholdId = householdId
        };

        Assert.Equal(id, item.Id);
        Assert.Equal("Groceries", item.Name);
        Assert.Null(item.Description);
        Assert.Equal(84.13, item.Price);
        Assert.Equal(householdId, item.HouseholdId);
    }

    [Fact]
    public void Item_DescriptionMayBeProvided()
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = "Groceries",
            Description = "Weekly shop",
            Price = 84.13,
            HouseholdId = Guid.NewGuid()
        };

        Assert.Equal("Weekly shop", item.Description);
    }

    [Fact]
    public void UserHousehold_LinksUserAndHousehold()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var householdId = Guid.NewGuid();

        var membership = new UserHousehold
        {
            Id = id,
            UserId = userId,
            HouseholdId = householdId
        };

        Assert.Equal(id, membership.Id);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(householdId, membership.HouseholdId);
    }

    [Fact]
    public void Goal_CanBeConstructedWithTotal()
    {
        var id = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 9, 1);

        var goal = new Goal
        {
            Id = id,
            Name = "Emergency fund",
            Value = 5000.00,
            StartDate = start,
            EndDate = end,
            Recurring = true,
            Interval = Interval.Quarterly,
            HouseholdId = householdId,
            UserId = userId,
            Total = 1250.00
        };

        Assert.Equal(id, goal.Id);
        Assert.Equal("Emergency fund", goal.Name);
        Assert.Equal(5000.00, goal.Value);
        Assert.Equal(start, goal.StartDate);
        Assert.Equal(end, goal.EndDate);
        Assert.True(goal.Recurring);
        Assert.Equal(Interval.Quarterly, goal.Interval);
        Assert.Equal(householdId, goal.HouseholdId);
        Assert.Equal(userId, goal.UserId);
        Assert.Equal(1250.00, goal.Total);
    }

    [Fact]
    public void Goal_DeserializesNullIntervalAsNull()
    {
        const string json = """
            {
              "id": "9b2f3c1e-4d5a-4e6f-8a7b-0c1d2e3f4a5b",
              "name": "Emergency fund",
              "value": 10000,
              "startDate": "2026-01-01T00:00:00Z",
              "endDate": "2026-12-31T00:00:00Z",
              "recurring": false,
              "interval": null,
              "householdId": "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
              "userId": "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e",
              "total": 0
            }
            """;

        var goal = JsonSerializer.Deserialize<Goal>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(goal);
        Assert.Null(goal!.Interval);
    }

    [Fact]
    public void Goal_RoundTripsDefinedInterval()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var goal = new Goal
        {
            Name = "Holiday",
            Value = 1000.00,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = true,
            Interval = Interval.BiWeekly
        };

        var json = JsonSerializer.Serialize(goal, options);
        var roundTripped = JsonSerializer.Deserialize<Goal>(json, options);

        Assert.Contains("\"interval\":2", json);
        Assert.NotNull(roundTripped);
        Assert.Equal(Interval.BiWeekly, roundTripped!.Interval);
    }
}
