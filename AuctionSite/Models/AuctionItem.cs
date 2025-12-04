using System;
using System.ComponentModel.DataAnnotations;

namespace AuctionSite.Models
{
    public class AuctionItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(0, 100000000)]
        public decimal StartPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
