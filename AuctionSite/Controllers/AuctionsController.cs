using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuctionSite.Models;
using AuctionSite.Models.ViewModels;

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

        // ★ GET: /Auctions/Details/5
// GET: /Auctions/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var item = await _context.AuctionItems
                .Include(a => a.Bids)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            var bids = item.Bids
                .OrderByDescending(b => b.CreatedAt)
                .ToList();

            var currentPrice = bids.Any()
                ? bids.Max(b => b.Amount)
                : item.StartPrice;

            var vm = new AuctionDetailsViewModel
            {
                Item = item,
                Bids = bids,
                CurrentPrice = currentPrice
            };

            return View(vm);
        }

        // POST: /Auctions/PlaceBid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceBid(int auctionItemId, string bidderName, decimal amount)
        {
            // 対象商品の存在チェック
            var item = await _context.AuctionItems
                .Include(a => a.Bids)
                .FirstOrDefaultAsync(a => a.Id == auctionItemId);

            if (item == null)
            {
                return NotFound();
            }

            // 現在価格を算出
            var currentPrice = item.Bids.Any()
                ? item.Bids.Max(b => b.Amount)
                : item.StartPrice;

            // 簡易バリデーション：現在価格以上でないと入札不可
            if (amount <= currentPrice)
            {
                TempData["BidError"] = $"現在価格 {currentPrice} より高い金額で入札してください。";
                return RedirectToAction(nameof(Details), new { id = auctionItemId });
            }

            if (string.IsNullOrWhiteSpace(bidderName))
            {
                TempData["BidError"] = "入札者名を入力してください。";
                return RedirectToAction(nameof(Details), new { id = auctionItemId });
            }

            var bid = new Bid
            {
                AuctionItemId = auctionItemId,
                BidderName = bidderName.Trim(),
                Amount = amount,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bids.Add(bid);
            await _context.SaveChangesAsync();

            TempData["BidMessage"] = "入札が完了しました。";
            return RedirectToAction(nameof(Details), new { id = auctionItemId });
        }



                // ★ GET: /Auctions/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.AuctionItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        // ★ POST: /Auctions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AuctionItem item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(item);
            }

            try
            {
                _context.Update(item);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.AuctionItems.AnyAsync(a => a.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // ★ GET: /Auctions/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.AuctionItems
                .FirstOrDefaultAsync(a => a.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        // ★ POST: /Auctions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.AuctionItems.FindAsync(id);
            if (item != null)
            {
                _context.AuctionItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
