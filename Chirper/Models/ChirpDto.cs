using Microsoft.AspNetCore.Mvc.Rendering;

namespace Chirper.Models
{
    public class ChirpDto : Chirp
    {
        public string Username { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string GravatarCode { get; set; } = null!;
        public TagList[]? Tags { get; set; }
        public string[]? SelectedTags { get; set; }
    }
}
