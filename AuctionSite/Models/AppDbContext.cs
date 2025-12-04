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
    }
}
