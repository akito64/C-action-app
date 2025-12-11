using System;
using System.ComponentModel.DataAnnotations;

namespace AuctionSite.Models
{
    public class Bid
    {
        public int Id { get; set; }

        [Required]
        public int AuctionItemId { get; set; }

        // ナビゲーションプロパティ
        public AuctionItem? AuctionItem { get; set; }

        [Required]
        [StringLength(50)]
        public string BidderName { get; set; } = string.Empty;

        [Range(1, 100000000)]
        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
