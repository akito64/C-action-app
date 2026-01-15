using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuctionSite.Models;
using AuctionSite.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using AuctionSite.Hubs;


namespace AuctionSite.Controllers
{
    public class AuctionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<AuctionHub> _hub;

        public AuctionsController(AppDbContext context, IWebHostEnvironment env, IHubContext<AuctionHub> hub)
        {
            _context = context;
            _env = env;
            _hub = hub;
        }

        private int? GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(id, out var userId))
            {
                return userId;
            }
            return null;
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
        [Authorize]
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
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return View(item);
            }

            // 日本時間の「いま」
            var nowJst = DateTime.UtcNow.AddHours(9);
            
            // DBには UTC を保存（今まで通り）
            item.CreatedAt = DateTime.UtcNow;
            item.SellerId = userId;

            // 終了日時チェック（EndTime は日本時間で入力されている前提）
            if (item.EndTime <= nowJst)
            {
                ModelState.AddModelError(nameof(item.EndTime), "終了日時は現在より後の日時を指定してください。");
                return View(item);
            }

            // 画像アップロード
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

            // ★ ここで「新しい出品が追加されたよ」と全クライアントに通知
            await _hub.Clients.All.SendAsync("AuctionUpdated");

            return RedirectToAction(nameof(Index));
        }

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

            // 終了済みなら入札不可
            if (item.IsEnded)
            {
                TempData["BidError"] = "このオークションは既に終了しています。";
                return RedirectToAction(nameof(Details), new { id = auctionItemId });
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

            // ★ 入札があったことを通知
            await _hub.Clients.All.SendAsync("AuctionUpdated");

            return RedirectToAction(nameof(Details), new { id = auctionItemId });
        }


        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                // ログインしてなければログイン画面へ
                return Challenge();
            }

            var item = await _context.AuctionItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            // ★ 出品者以外は 403
            if (item.SellerId != userId)
            {
                return Forbid();
            }

            return View(item);
        }


        // POST: /Auctions/Edit/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AuctionItem formItem, IFormFile? imageFile)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var item = await _context.AuctionItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            // ★ 出品者チェック
            if (item.SellerId != userId)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                // バリデーションエラー時は元の item を渡す
                return View(item);
            }

            // ★ 変更を許可する項目だけ上書き
            item.Title       = formItem.Title;
            item.Description = formItem.Description;
            item.StartPrice  = formItem.StartPrice;
            item.EndTime     = formItem.EndTime;

            // 画像アップロード処理（今までのロジックを item に対して書き換え）
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

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // GET: /Auctions/Delete/5
        // GET: /Auctions/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var item = await _context.AuctionItems
                .FirstOrDefaultAsync(a => a.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            // ★ 出品者チェック
            if (item.SellerId != userId)
            {
                return Forbid();
            }

            return View(item);
        }

        // POST: /Auctions/Delete/5
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var item = await _context.AuctionItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            // ★ 出品者以外は削除NG
            if (item.SellerId != userId)
            {
                return Forbid();
            }

            _context.AuctionItems.Remove(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // マイページ
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

            var now = DateTime.UtcNow.AddHours(9);   // 日本時間


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
