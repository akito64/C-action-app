using System;
using System.Threading;
using AuctionSite.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json から接続文字列を取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ★ MySQL のバージョンを固定指定（8.0 系ならこれでOK）
var serverVersion = new MySqlServerVersion(new Version(8, 0, 43));
// バージョン細かく気にしないなら new Version(8, 0, 0) でもOK

// DbContext 登録
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ★ MySQL コンテナが立ち上がるまで少し待つ（雑だけどシンプルな対策）
Thread.Sleep(5000);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// HTTPS リダイレクトは開発用に一旦オフ
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// デフォルトルート: /Auctions/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auctions}/{action=Index}/{id?}");

app.Run();
