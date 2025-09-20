using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Dtos
{
    public class ProductQuantityItemDto
    {
        [Required]
        public int ProductId { get; set; }
        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }
}
