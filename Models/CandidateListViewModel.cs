using System.Collections.Generic;

namespace ElectionAdminPanel.Web.Models
{
    public class CandidateListViewModel
    {
        public IEnumerable<CandidateModel> Candidates { get; set; } = new List<CandidateModel>();
        public string Search { get; set; } = string.Empty;
        public int? PositionId { get; set; }
        public bool? IsActive { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
