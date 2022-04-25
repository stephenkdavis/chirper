using System;
using System.Collections.Generic;

namespace Chirper.Models
{
    public partial class Chirp
    {
        public Guid ChirpId { get; set; }
        public Guid UserId { get; set; }
        public DateTime ChirpTimestamp { get; set; }
        public string ChirpBody { get; set; } = null!;
        public long ChirpLikes { get; set; }
        public long ChirpDislikes { get; set; }
    }
}
