using System;
using System.Collections.Generic;

namespace ElectionAdminPanel.Web.Models
{
    public class ReportViewModel
    {
        public AuditLogListResponse AuditLogs { get; set; } = new AuditLogListResponse();
        public AuditLogListResponse AdminLogs { get; set; } = new AuditLogListResponse();
        public AuditLogListResponse VoterLogs { get; set; } = new AuditLogListResponse();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string UserType { get; set; } = "";
    }

    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
    }

    public class AuditLogListResponse
    {
        public List<AuditLog> Items { get; set; } = new List<AuditLog>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
