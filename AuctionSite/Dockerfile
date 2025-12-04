# ビルド用イメージ
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# プロジェクト一式コピー
COPY . ./

# Publish（Releaseビルドして /app/publish に出力）
RUN dotnet publish -c Release -o /app/publish

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# ビルド成果物をコピー
COPY --from=build /app/publish .

# ASP.NET Core を 8080 ポートで待ち受け
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AuctionSite.dll"]
