using System.ComponentModel.DataAnnotations;

namespace Chirper.Models
{
    public class UserDetailsDto : User
    {
        [Display(Name = "Number of Chirps")]
        public long ChirpCount { get; set; }

        [Display(Name = "Number of Followers")]
        public long FollowerCount { get; set; }

        [Display(Name = "Number of Likes")]
        public long LikeCount { get; set; }
        public string? GravatarCode { get; set; }
    }
}
