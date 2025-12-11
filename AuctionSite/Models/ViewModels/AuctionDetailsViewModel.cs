using System.Collections.Generic;

namespace AuctionSite.Models.ViewModels
{
    public class AuctionDetailsViewModel
    {
        public AuctionItem Item { get; set; } = null!;
        public List<Bid> Bids { get; set; } = new();

        public decimal CurrentPrice { get; set; }
    }
}
