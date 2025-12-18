using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuctionSite.Models;
using AuctionSite.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionSite.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // ===== 共通ヘルパー =====

        private int? GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(id, out var userId))
            {
                return userId;
            }
            return null;
        }

        private bool IsAdult(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }
            return age >= 18;
        }

        private async Task SignInUserAsync(User user, bool isPersistent = false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);
        }

        // ===== 新規登録 =====

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!IsAdult(model.BirthDate))
            {
                ModelState.AddModelError(nameof(model.BirthDate), "18歳未満の方は登録できません。");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "このメールアドレスは既に登録されています。");
                return View(model);
            }

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                BirthDate = model.BirthDate,
                PasswordHash = PasswordHelper.HashPassword(model.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await SignInUserAsync(user);

            return RedirectToAction("Index", "Auctions");
        }

        // ===== ログイン =====

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "メールアドレスまたはパスワードが正しくありません。");
                return View(model);
            }

            await SignInUserAsync(user, model.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Auctions");
        }

        // ===== ログアウト =====

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Auctions");
        }

        // ===== マイページ =====

        [Authorize]
        [HttpGet]
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

            var biddingItems = await _context.Bids
                .Where(b => b.UserId == userId)
                .Include(b => b.AuctionItem)
                .Select(b => b.AuctionItem!)
                .Distinct()
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

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
