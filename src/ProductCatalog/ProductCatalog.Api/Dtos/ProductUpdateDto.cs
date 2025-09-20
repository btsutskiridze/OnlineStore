using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Dtos
{
    public class ProductUpdateDto
    {
        [Required]
        public string RowVersionBase64 { get; set; }

        [MaxLength(20)]
        public string? SKU { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? Price { get; set; }

        [Range(0, int.MaxValue)]
        public int? StockQuantity { get; set; }

        public bool? IsActive { get; set; }
    }
}
