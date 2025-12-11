using System;
using System.ComponentModel.DataAnnotations;

namespace AuctionSite.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "名前")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "メールアドレス")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "生年月日")]
        public DateTime BirthDate { get; set; }

        // 平文パスワードは保存せず、ハッシュのみ保存
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
    }
}
