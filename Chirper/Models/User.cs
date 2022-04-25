using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Chirper.Models
{
    public partial class User
    {
        public Guid UserId { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "A user name is required.")]
        [DataType(DataType.Text)]
        [Display(Name = "Username")]
        public string Username { get; set; } = null!;

        [Required(AllowEmptyStrings = false, ErrorMessage = "An email address is required.")]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Email Address")]
        [RegularExpression("^[a-z0-9_\\+-]+(\\.[a-z0-9_\\+-]+)*@[a-z0-9-]+(\\.[a-z0-9]+)*\\.([a-z]{2,4})$", ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = null!;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Your first name is required.")]
        [DataType(DataType.Text)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = null!;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Your last name is required.")]
        [DataType(DataType.Text)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = null!;

        [Display(Name = "Join Date")]
        public DateOnly JoinDate { get; set; }

        public bool AccountActive { get; set; }

        public string? PwdHash { get; set; }

        public string? ActivationKey { get; set; }

        public DateTime? PwdResetTimestamp { get; set; }
    }
}
