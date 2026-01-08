using System;
using System.Linq;
using System.Threading;
using AuctionSite.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using AuctionSite.Hubs;



var builder = WebApplication.CreateBuilder(args);

// appsettings.json から接続文字列を取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// MySQL 8.0 系のバージョン指定
var serverVersion = new MySqlServerVersion(new Version(8, 0, 43));

// ★ DB コンテキスト登録（リトライ有効）
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(
        connectionString,
        serverVersion,
        mySqlOptions =>
        {
            // 一時的な接続失敗時に自動リトライ
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
});

builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();   // ★ 追加

// Cookie 認証を追加
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });


var app = builder.Build();

// ★ DB 初期化（EnsureCreated + サンプルデータ）をリトライ付きで実行
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    const int maxTry = 5;
    for (int i = 1; i <= maxTry; i++)
    {
        try
        {
            Console.WriteLine($"[DB INIT] Try {i}/{maxTry} ...");

            // DB とテーブルがなければ作成
            db.Database.EnsureCreated();

            // サンプルデータ投入（AuctionItems が空のときだけ）
            if (!db.AuctionItems.Any())
            {
                db.AuctionItems.Add(new AuctionItem
                {
                    Title = "サンプル商品A",
                    Description = "Docker + C# 用のサンプル商品です。",
                    StartPrice = 1000m,
                    CreatedAt = DateTime.UtcNow
                });

                db.AuctionItems.Add(new AuctionItem
                {
                    Title = "サンプル商品B",
                    Description = "講義用の二つ目のサンプル商品です。",
                    StartPrice = 2500m,
                    CreatedAt = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            Console.WriteLine("[DB INIT] Success");
            break; // 成功したのでループ抜ける
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB INIT] Failed (try {i}) : {ex.Message}");

            if (i == maxTry)
            {
                Console.WriteLine("[DB INIT] Give up. Application will start without DB initialized.");
                // ここで throw し直すとまたコンテナが落ちるので、必要なら throw でもOK
                break;
            }

            // 次のトライまで少し待つ
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 開発用に HTTPS リダイレクトはオフのままでOK
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); 
app.UseAuthorization();

// デフォルトルート: /Auctions/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auctions}/{action=Index}/{id?}");

app.MapHub<AuctionHub>("/auctionHub");   // ★ 追加


app.Run();
