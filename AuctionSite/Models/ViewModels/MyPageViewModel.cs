using System.Collections.Generic;

namespace AuctionSite.Models.ViewModels
{
    public class MyPageViewModel
    {
        public User User { get; set; } = null!;
        public List<AuctionItem> SellingItems { get; set; } = new();
        public List<AuctionItem> BiddingItems { get; set; } = new();
        public List<AuctionItem> WonItems { get; set; } = new();
    }
}
