using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Chirper.Models
{
    public partial class Comment
    {
        public Guid CommentId { get; set; }
        public Guid ChirpId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CommentTimestamp { get; set; }
        [MaxLength(500, ErrorMessage = "Comments can not exceed 500 characters.")]
        [Display(Name = "Comment")]
        public string CommentBody { get; set; } = null!;
    }
}
