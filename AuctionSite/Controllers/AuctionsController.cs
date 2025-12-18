using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuctionSite.Models;
using AuctionSite.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.IO;


namespace AuctionSite.Controllers
{
    public class AuctionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private int? GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(id, out var userId))
            {
                return userId;
            }
            return null;
        }

        public AuctionsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AuctionItem item, IFormFile? imageFile)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge(); // ログイン画面へ
            }

            if (!ModelState.IsValid)
            {
                return View(item);
            }

            item.CreatedAt = DateTime.UtcNow;
            item.SellerId = userId;

            // ★ 画像アップロード処理
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);

                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await imageFile.CopyToAsync(stream);
                }

                item.ImageFileName = fileName;
            }

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
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> PlaceBid(int auctionItemId, decimal amount)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var item = await _context.AuctionItems
                .Include(a => a.Bids)
                .FirstOrDefaultAsync(a => a.Id == auctionItemId);

            if (item == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return Forbid();
            }

            var currentPrice = item.Bids.Any()
                ? item.Bids.Max(b => b.Amount)
                : item.StartPrice;

            if (amount <= currentPrice)
            {
                TempData["BidError"] = $"現在価格 {currentPrice} より高い金額で入札してください。";
                return RedirectToAction(nameof(Details), new { id = auctionItemId });
            }

            var bid = new Bid
            {
                AuctionItemId = auctionItemId,
                UserId = user.Id,
                BidderName = user.Name,
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
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Edit(int id, AuctionItem item, IFormFile? imageFile)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(item);
            }

            // 画像が指定されていれば新しいファイルを保存
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);

                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await imageFile.CopyToAsync(stream);
                }

                item.ImageFileName = fileName;
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

        [Authorize]
        public async Task<IActionResult> MyPage()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            var now = DateTime.UtcNow;

            var sellingItems = await _context.AuctionItems
                .Where(a => a.SellerId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // 自分が入札した商品
            var biddingItems = await _context.Bids
                .Where(b => b.UserId == userId)
                .Include(b => b.AuctionItem)
                .Select(b => b.AuctionItem!)
                .Distinct()
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // 落札した商品（終了済み & 自分が最高額入札者）
            var finishedItems = await _context.AuctionItems
                .Include(a => a.Bids)
                .Where(a => a.EndTime <= now && a.Bids.Any())
                .ToListAsync();

            var wonItems = finishedItems
                .Where(a =>
                {
                    var topBid = a.Bids
                        .OrderByDescending(b => b.Amount)
                        .ThenByDescending(b => b.CreatedAt)
                        .FirstOrDefault();
                    return topBid?.UserId == userId;
                })
                .OrderByDescending(a => a.EndTime)
                .ToList();

            var vm = new MyPageViewModel
            {
                User = user,
                SellingItems = sellingItems,
                BiddingItems = biddingItems,
                WonItems = wonItems
            };

            return View(vm);
        }
    }
}
