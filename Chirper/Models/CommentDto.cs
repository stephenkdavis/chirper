namespace Chirper.Models
{
    public class CommentDto : Comment
    {
        public string Username { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string GravatarCode { get; set; } = null!;
    }
}
