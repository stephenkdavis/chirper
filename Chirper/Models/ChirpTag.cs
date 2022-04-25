using System;
using System.Collections.Generic;

namespace Chirper.Models
{
    public partial class ChirpTag
    {
        public Guid EntryId { get; set; }
        public Guid ChripId { get; set; }
        public Guid TagId { get; set; }
    }
}
