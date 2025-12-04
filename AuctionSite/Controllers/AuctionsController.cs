using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuctionSite.Models;

namespace AuctionSite.Controllers
{
    public class AuctionsController : Controller
    {
        private readonly AppDbContext _context;

        public AuctionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Auctions
        public async Task<IActionResult> Index()
        {
            var items = await _context.AuctionItems
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // GET: /Auctions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Auctions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AuctionItem item)
        {
            if (!ModelState.IsValid)
            {
                return View(item);
            }

            item.CreatedAt = DateTime.UtcNow;
            _context.AuctionItems.Add(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
