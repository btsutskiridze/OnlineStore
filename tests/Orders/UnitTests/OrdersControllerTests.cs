using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Orders.Api.Controllers;
using Orders.Api.Dtos;
using Orders.Api.Services.Contracts;

namespace Orders.UnitTests;

public class OrdersControllerTests
{
    private readonly Mock<IOrdersReadService> _mockOrdersReadService;
    private readonly Mock<IOrdersCreationService> _mockOrdersCreationService;
    private readonly Mock<IOrdersCancellationService> _mockOrdersCancellationService;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _mockOrdersReadService = new Mock<IOrdersReadService>();
        _mockOrdersCreationService = new Mock<IOrdersCreationService>();
        _mockOrdersCancellationService = new Mock<IOrdersCancellationService>();

        _controller = new OrdersController(
            _mockOrdersReadService.Object,
            _mockOrdersCreationService.Object,
            _mockOrdersCancellationService.Object);
    }

    [Fact]
    public async Task CreateOrder_WithValidData_ReturnsCreatedAtActionWithOrder()
    {
        const string idempotencyKey = "test-key-123";
        var items = new List<ProductQuantityItemDto>
        {
            new() { ProductId = 1, Quantity = 2 },
            new() { ProductId = 2, Quantity = 3 }
        };

        var expectedOrder = new OrderDetailsDto
        {
            Guid = Guid.NewGuid(),
            UserId = "user123",
            Status = "Pending",
            TotalAmount = 149.98m,
            CreatedAt = DateTime.UtcNow,
            RowVersionBase64 = "AAAAAAAAB9E=",
            Items = new List<OrderItemDetailsDto>
            {
                new()
                {
                    ProductId = 1,
                    ProductName = "Product 1",
                    Quantity = 2,
                    UnitPrice = 29.99m,
                    SKU = "SKU001",
                    LineTotal = 59.98m
                },
                new()
                {
                    ProductId = 2,
                    ProductName = "Product 2",
                    Quantity = 3,
                    UnitPrice = 30.00m,
                    SKU = "SKU002",
                    LineTotal = 90.00m
                }
            }
        };

        _mockOrdersCreationService
            .Setup(x => x.CreateOrderAsync(idempotencyKey, items, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var result = await _controller.CreateOrder(idempotencyKey, items, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(OrdersController.GetOrderById));
        createdResult.RouteValues!["guid"].Should().Be(expectedOrder.Guid);
        createdResult.Value.Should().BeEquivalentTo(expectedOrder);

        _mockOrdersCreationService.Verify(
            x => x.CreateOrderAsync(idempotencyKey, items, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithEmptyItems_ThrowsException()
    {
        const string idempotencyKey = "test-key-123";
        var emptyItems = new List<ProductQuantityItemDto>();

        _mockOrdersCreationService
            .Setup(x => x.CreateOrderAsync(idempotencyKey, emptyItems, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No items provided."));

        await FluentActions.Invoking(() => _controller.CreateOrder(idempotencyKey, emptyItems, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No items provided.");

        _mockOrdersCreationService.Verify(
            x => x.CreateOrderAsync(idempotencyKey, emptyItems, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrderById_WithValidGuid_ReturnsOkWithOrder()
    {
        var orderGuid = Guid.NewGuid();
        var expectedOrder = new OrderDetailsDto
        {
            Guid = orderGuid,
            UserId = "user123",
            Status = "Pending",
            TotalAmount = 99.99m,
            CreatedAt = DateTime.UtcNow,
            RowVersionBase64 = "AAAAAAAAB9E=",
            Items = new List<OrderItemDetailsDto>
            {
                new()
                {
                    ProductId = 1,
                    ProductName = "Test Product",
                    Quantity = 1,
                    UnitPrice = 99.99m,
                    SKU = "TEST001",
                    LineTotal = 99.99m
                }
            }
        };

        _mockOrdersReadService
            .Setup(x => x.GetOrderByIdAsync(orderGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var result = await _controller.GetOrderById(orderGuid, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedOrder);

        _mockOrdersReadService.Verify(
            x => x.GetOrderByIdAsync(orderGuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrderById_WithNonExistentGuid_ReturnsNull()
    {
        var nonExistentGuid = Guid.NewGuid();

        _mockOrdersReadService
            .Setup(x => x.GetOrderByIdAsync(nonExistentGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailsDto?)null);

        var result = await _controller.GetOrderById(nonExistentGuid, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeNull();

        _mockOrdersReadService.Verify(
            x => x.GetOrderByIdAsync(nonExistentGuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrdersByUserId_ReturnsOkWithOrdersList()
    {
        var expectedOrders = new List<OrderListItemDto>
        {
            new()
            {
                Guid = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                TotalAmount = 59.98m,
                ItemCount = 2,
                Status = "Completed"
            },
            new()
            {
                Guid = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                TotalAmount = 149.99m,
                ItemCount = 3,
                Status = "Pending"
            }
        };

        _mockOrdersReadService
            .Setup(x => x.GetOrdersByUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrders);

        var result = await _controller.GetOrdersByUserId(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedOrders);

        _mockOrdersReadService.Verify(
            x => x.GetOrdersByUserIdAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrdersByUserId_WithNoOrders_ReturnsEmptyList()
    {
        var emptyOrdersList = new List<OrderListItemDto>();

        _mockOrdersReadService
            .Setup(x => x.GetOrdersByUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyOrdersList);

        var result = await _controller.GetOrdersByUserId(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(emptyOrdersList);

        _mockOrdersReadService.Verify(
            x => x.GetOrdersByUserIdAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WithValidData_ReturnsNoContent()
    {
        var orderGuid = Guid.NewGuid();
        var cancelDto = new OrderCancelDto
        {
            RowVersionBase64 = "AAAAAAAAB9E="
        };

        _mockOrdersCancellationService
            .Setup(x => x.CancelOrderAsync(orderGuid, cancelDto.RowVersionBase64, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.CancelOrder(orderGuid, cancelDto, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mockOrdersCancellationService.Verify(
            x => x.CancelOrderAsync(orderGuid, cancelDto.RowVersionBase64, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WithInvalidRowVersion_ThrowsException()
    {
        var orderGuid = Guid.NewGuid();
        var cancelDto = new OrderCancelDto
        {
            RowVersionBase64 = "invalid-row-version"
        };

        _mockOrdersCancellationService
            .Setup(x => x.CancelOrderAsync(orderGuid, cancelDto.RowVersionBase64, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid row version format."));

        await FluentActions.Invoking(() => _controller.CancelOrder(orderGuid, cancelDto, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid row version format.");

        _mockOrdersCancellationService.Verify(
            x => x.CancelOrderAsync(orderGuid, cancelDto.RowVersionBase64, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WithNonExistentOrder_ThrowsException()
    {
        var nonExistentGuid = Guid.NewGuid();
        var cancelDto = new OrderCancelDto
        {
            RowVersionBase64 = "AAAAAAAAB9E="
        };

        _mockOrdersCancellationService
            .Setup(x => x.CancelOrderAsync(nonExistentGuid, cancelDto.RowVersionBase64, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Order not found."));

        await FluentActions.Invoking(() => _controller.CancelOrder(nonExistentGuid, cancelDto, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Order not found.");

        _mockOrdersCancellationService.Verify(
            x => x.CancelOrderAsync(nonExistentGuid, cancelDto.RowVersionBase64, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithMultipleItemsSameProduct_ConsolidatesQuantities()
    {
        const string idempotencyKey = "consolidate-test-123";
        var items = new List<ProductQuantityItemDto>
        {
            new() { ProductId = 1, Quantity = 2 },
            new() { ProductId = 1, Quantity = 3 },
            new() { ProductId = 2, Quantity = 1 }
        };

        var expectedOrder = new OrderDetailsDto
        {
            Guid = Guid.NewGuid(),
            UserId = "user123",
            Status = "Pending",
            TotalAmount = 179.97m,
            CreatedAt = DateTime.UtcNow,
            RowVersionBase64 = "AAAAAAAAB9E=",
            Items = new List<OrderItemDetailsDto>
            {
                new()
                {
                    ProductId = 1,
                    ProductName = "Product 1",
                    Quantity = 5,
                    UnitPrice = 29.99m,
                    SKU = "SKU001",
                    LineTotal = 149.95m
                },
                new()
                {
                    ProductId = 2,
                    ProductName = "Product 2",
                    Quantity = 1,
                    UnitPrice = 30.02m,
                    SKU = "SKU002",
                    LineTotal = 30.02m
                }
            }
        };

        _mockOrdersCreationService
            .Setup(x => x.CreateOrderAsync(idempotencyKey, items, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var result = await _controller.CreateOrder(idempotencyKey, items, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.Value.Should().BeEquivalentTo(expectedOrder);

        _mockOrdersCreationService.Verify(
            x => x.CreateOrderAsync(idempotencyKey, items, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
