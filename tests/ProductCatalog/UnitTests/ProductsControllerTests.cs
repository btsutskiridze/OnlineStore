using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductCatalog.Api.Controllers;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Responses;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.UnitTests;

public class ProductsControllerTests
{
    private readonly Mock<IProductManagementService> _mockProductManagementService;
    private readonly Mock<IProductStockService> _mockProductStockService;
    private readonly Mock<IProductValidationService> _mockProductValidationService;
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _mockProductManagementService = new Mock<IProductManagementService>();
        _mockProductStockService = new Mock<IProductStockService>();
        _mockProductValidationService = new Mock<IProductValidationService>();

        _controller = new ProductsController(
            _mockProductManagementService.Object,
            _mockProductStockService.Object,
            _mockProductValidationService.Object);
    }

    [Fact]
    public async Task List_WithDefaultParameters_ReturnsOkWithProducts()
    {
        var expectedResponse = new PagedResponse<ProductListItemDto>
        {
            Items = new List<ProductListItemDto>
            {
                new() { Id = 1, Name = "Product 1", SKU = "SKU001", Price = 10.99m, StockQuantity = 50, IsActive = true },
                new() { Id = 2, Name = "Product 2", SKU = "SKU002", Price = 20.99m, StockQuantity = 30, IsActive = true }
            },
            PageNumber = 1,
            PageSize = 20,
            TotalCount = 2,
            TotalPages = 1,
            HasPrevious = false,
            HasNext = false
        };

        _mockProductManagementService
            .Setup(x => x.GetAllProducts(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.List();

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);

        _mockProductManagementService.Verify(
            x => x.GetAllProducts(1, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task List_WithCustomParameters_ReturnsOkWithProducts()
    {
        const int page = 2;
        const int pageSize = 10;
        var expectedResponse = new PagedResponse<ProductListItemDto>
        {
            Items = new List<ProductListItemDto>(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 1,
            HasPrevious = true,
            HasNext = false
        };

        _mockProductManagementService
            .Setup(x => x.GetAllProducts(page, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.List(page, pageSize);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);

        _mockProductManagementService.Verify(
            x => x.GetAllProducts(page, pageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Details_WithValidId_ReturnsOkWithProduct()
    {
        const int productId = 1;
        var expectedProduct = new ProductDetailsDto
        {
            Id = productId,
            Name = "Test Product",
            SKU = "TEST001",
            Price = 29.99m,
            StockQuantity = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            RowVersion = "AAAAAAAAB9E="
        };

        _mockProductManagementService
            .Setup(x => x.GetProductById(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProduct);

        var result = await _controller.Details(productId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedProduct);

        _mockProductManagementService.Verify(
            x => x.GetProductById(productId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_WithValidDto_ReturnsCreatedAtActionWithProduct()
    {
        var createDto = new ProductCreateDto
        {
            Name = "New Product",
            SKU = "NEW001",
            Price = 39.99m,
            StockQuantity = 50,
            IsActive = true
        };

        var createdProduct = new ProductDetailsDto
        {
            Id = 123,
            Name = createDto.Name,
            SKU = createDto.SKU,
            Price = createDto.Price,
            StockQuantity = createDto.StockQuantity,
            IsActive = createDto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            RowVersion = "AAAAAAAAB9E="
        };

        _mockProductManagementService
            .Setup(x => x.CreateProduct(createDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProduct);

        var result = await _controller.Create(createDto, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(ProductsController.Details));
        createdResult.RouteValues!["id"].Should().Be(createdProduct.Id);
        createdResult.Value.Should().BeEquivalentTo(createdProduct);

        _mockProductManagementService.Verify(
            x => x.CreateProduct(createDto, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_WithValidDto_ReturnsOkWithUpdatedProduct()
    {
        const int productId = 1;
        var updateDto = new ProductUpdateDto
        {
            RowVersionBase64 = "AAAAAAAAB9E=",
            SKU = "UPDATED001",
            Price = 49.99m,
            StockQuantity = 75,
            IsActive = false
        };

        var updatedProduct = new ProductDetailsDto
        {
            Id = productId,
            Name = "Existing Product",
            SKU = updateDto.SKU!,
            Price = updateDto.Price!.Value,
            StockQuantity = updateDto.StockQuantity!.Value,
            IsActive = updateDto.IsActive!.Value,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            RowVersion = "AAAAAAAAB9F="
        };

        _mockProductManagementService
            .Setup(x => x.UpdateProduct(productId, updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedProduct);

        var result = await _controller.Update(productId, updateDto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(updatedProduct);

        _mockProductManagementService.Verify(
            x => x.UpdateProduct(productId, updateDto, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Validate_WithValidItems_ReturnsOkWithValidationResults()
    {
        var items = new List<ProductQuantityItemDto>
        {
            new() { ProductId = 1, Quantity = 5 },
            new() { ProductId = 2, Quantity = 10 }
        };

        var expectedResults = new List<ProductValidationResultDto>
        {
            new()
            {
                ProductId = 1,
                RequestedQuantity = 5,
                Exists = true,
                CanFulfill = true,
                Name = "Product 1",
                Sku = "SKU001",
                Price = 10.99m
            },
            new()
            {
                ProductId = 2,
                RequestedQuantity = 10,
                Exists = true,
                CanFulfill = false,
                Name = "Product 2",
                Sku = "SKU002",
                Price = 20.99m
            }
        };

        _mockProductValidationService
            .Setup(x => x.ValidateProducts(items, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var result = await _controller.Validate(items, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResults);

        _mockProductValidationService.Verify(
            x => x.ValidateProducts(items, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Decrement_WithValidItemsAndIdempotencyKey_ReturnsNoContent()
    {
        const string idempotencyKey = "test-key-123";
        var items = new List<ProductQuantityItemDto>
        {
            new() { ProductId = 1, Quantity = 3 },
            new() { ProductId = 2, Quantity = 7 }
        };

        _mockProductStockService
            .Setup(x => x.DecrementStockBatch(idempotencyKey, items, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Decrement(idempotencyKey, items, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mockProductStockService.Verify(
            x => x.DecrementStockBatch(idempotencyKey, items, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Replenish_WithValidItemsAndIdempotencyKey_ReturnsNoContent()
    {
        const string idempotencyKey = "replenish-key-456";
        var items = new List<ProductQuantityItemDto>
        {
            new() { ProductId = 1, Quantity = 20 },
            new() { ProductId = 3, Quantity = 15 }
        };

        _mockProductStockService
            .Setup(x => x.ReplenishStockBatch(idempotencyKey, items, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Replenish(idempotencyKey, items, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mockProductStockService.Verify(
            x => x.ReplenishStockBatch(idempotencyKey, items, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
