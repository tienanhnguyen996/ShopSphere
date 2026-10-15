using FluentAssertions;
using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.BuildingBlocks.Common.Results;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public class DomainPrimitivesTests
{
    private sealed class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) : base(id) { }
    }

    private sealed class TestStringEntity : Entity<string>
    {
        public TestStringEntity(string id) : base(id) { }
    }

    private record TestDomainEvent(string Name) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id) : base(id) { }

        public void DoSomething(string name)
        {
            AddDomainEvent(new TestDomainEvent(name));
        }
    }

    private sealed class Address : ValueObject
    {
        public string Street { get; }
        public string City { get; }

        public Address(string street, string city)
        {
            Street = street;
            City = city;
        }

        protected override IEnumerable<object?> GetAtomicValues()
        {
            yield return Street;
            yield return City;
        }
    }

    [Fact]
    public void Entity_WithEmptyGuid_ThrowsArgumentException()
    {
        var act = () => new TestEntity(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Entity_WithEmptyOrWhitespaceString_ThrowsArgumentException(string? id)
    {
        var act = () => new TestStringEntity(id!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Entity_Equality_ComparesById()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);
        var entity3 = new TestEntity(Guid.NewGuid());

        (entity1 == entity2).Should().BeTrue();
        (entity1 != entity3).Should().BeTrue();
        entity1.Equals(entity2).Should().BeTrue();
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void AggregateRoot_ManagesDomainEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DomainEvents.Should().BeEmpty();

        aggregate.DoSomething("Created");
        aggregate.DomainEvents.Should().HaveCount(1);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ValueObject_StructuralEquality_WorksExpectedly()
    {
        var addr1 = new Address("123 Main St", "Metropolis");
        var addr2 = new Address("123 Main St", "Metropolis");
        var addr3 = new Address("456 Other St", "Metropolis");

        (addr1 == addr2).Should().BeTrue();
        (addr1 != addr3).Should().BeTrue();
        addr1.Equals(addr2).Should().BeTrue();
        addr1.Equals(addr3).Should().BeFalse();
        addr1.GetHashCode().Should().Be(addr2.GetHashCode());
    }

    [Fact]
    public void Result_Success_FlowsExpectedly()
    {
        var result = Result.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("hello");
        result.Error.Should().Be(Error.None);

        var matched = result.Match(
            onSuccess: val => $"Success: {val}",
            onFailure: err => $"Error: {err.Code}");

        matched.Should().Be("Success: hello");
    }

    [Fact]
    public void Result_Failure_FlowsExpectedly()
    {
        var error = Error.Validation("ITEM_INVALID", "The item is invalid.");
        var result = Result<string>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);

        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();

        var matched = result.Match(
            onSuccess: val => $"Success: {val}",
            onFailure: err => $"Error: {err.Code}");

        matched.Should().Be("Error: ITEM_INVALID");
    }

    [Fact]
    public void Result_NonGeneric_Match_WorksExpectedly()
    {
        var success = Result.Success();
        success.Match(() => "ok", _ => "fail").Should().Be("ok");

        var failure = Result.Failure(Error.NotFound("NOT_FOUND", "Missing"));
        failure.Match(() => "ok", err => err.Code).Should().Be("NOT_FOUND");
    }
}
