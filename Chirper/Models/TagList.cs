using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Chirper.Models
{
    public partial class TagList
    {
        public Guid TagId { get; set; }
        
        [Display(Name = "Tag Name")]
        public string TagName { get; set; } = null!;
    }
}
