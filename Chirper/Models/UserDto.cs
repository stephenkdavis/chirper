using System.ComponentModel.DataAnnotations;

namespace Chirper.Models
{
    public class UserDto : User
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = null!;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Confirm password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Your passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
