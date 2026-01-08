using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace AuctionSite.Models
{
    public class AuctionItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(0, 100000000)]
        public decimal StartPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //  追加: 終了日時（デフォルト3日後）
        [DataType(DataType.DateTime)]
        public DateTime EndTime { get; set; } = DateTime.UtcNow.AddDays(3);

        //  追加: 出品者
        public int? SellerId { get; set; }
        public User? Seller { get; set; }

        // 追加: 画像ファイル名（wwwroot/uploads 配下）
        public string? ImageFileName { get; set; }

        public List<Bid> Bids { get; set; } = new();

                // ----- ここから追加: ステータス系の計算プロパティ -----

        // 終了しているかどうか
        [NotMapped]
        public bool IsEnded => DateTime.UtcNow.AddHours(9) >= EndTime;

        // 残り時間（終了していたら null）
        [NotMapped]
        public TimeSpan? RemainingTime
        {
            get
            {
                var nowJst = DateTime.UtcNow.AddHours(9);
                return nowJst >= EndTime ? null : EndTime - nowJst;
            }
        }
    }
}
