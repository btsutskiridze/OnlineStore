using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Dtos
{
    public class ProductCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = default!;

        [Required]
        [MaxLength(20)]
        public string SKU { get; set; } = default!;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }
}
