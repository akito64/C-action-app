using Microsoft.EntityFrameworkCore;

namespace AuctionSite.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<AuctionItem> AuctionItems => Set<AuctionItem>();
        public DbSet<Bid> Bids => Set<Bid>();

        // ★ 追加：ユーザー
        public DbSet<User> Users => Set<User>();
    }
}
