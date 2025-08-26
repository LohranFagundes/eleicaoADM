using System.Collections.Generic;

namespace ElectionAdminPanel.Web.Models
{
    public class PositionListViewModel
    {
        public IEnumerable<PositionModel> Positions { get; set; } = new List<PositionModel>();
        public string Search { get; set; } = string.Empty;
        public int? ElectionId { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
