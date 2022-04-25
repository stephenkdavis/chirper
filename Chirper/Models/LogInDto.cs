using System.ComponentModel.DataAnnotations;

namespace Chirper.Models
{
    public class LogInDto
    {
        [Display(Name = "Username")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required.")]
        public string Username { get; set; } = null!;

        [Display(Name = "Password")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]

        public string Password { get; set; } = null!;
    }
}
