using System;
using System.Collections.Generic;

namespace Chirper.Models
{
    public partial class Follower
    {
        public Guid EntryId { get; set; }
        public Guid UserId { get; set; }
        public Guid FollowerId { get; set; }
    }
}
