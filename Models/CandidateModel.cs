using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ElectionAdminPanel.Web.Models
{
    public class CandidateModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(255, ErrorMessage = "O nome não pode exceder 255 caracteres.")]
        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O número é obrigatório.")]
        [StringLength(10, ErrorMessage = "O número não pode exceder 10 caracteres.")]
        [Display(Name = "Número")]
        public string Number { get; set; } = string.Empty;

        [Display(Name = "Descrição")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Biografia")]
        public string Biography { get; set; } = string.Empty;

        [Required(ErrorMessage = "O cargo é obrigatório.")]
        [Display(Name = "Cargo")]
        public int PositionId { get; set; }

        [Display(Name = "Ordem de Posição")]
        public int OrderPosition { get; set; }

        [Display(Name = "Ativo")]
        public bool IsActive { get; set; }

        [Display(Name = "URL da Foto")]
        public string PhotoUrl { get; set; } = string.Empty;

        [Display(Name = "Possui Foto")]
        public bool HasPhoto { get; set; }

        [Display(Name = "Possui Arquivo de Foto")]
        public bool HasPhotoFile { get; set; }

        [Display(Name = "Possui Foto BLOB")]
        public bool HasPhotoBlob { get; set; }

        [Display(Name = "Tipo de Armazenamento da Foto")]
        public string PhotoStorageType { get; set; } = string.Empty;

        [Display(Name = "Tipo MIME da Foto")]
        public string PhotoMimeType { get; set; } = string.Empty;

        [Display(Name = "Nome do Arquivo da Foto")]
        public string PhotoFileName { get; set; } = string.Empty;

        [Display(Name = "Contagem de Votos")]
        public int VotesCount { get; set; }

        [Display(Name = "Foto")]
        public IFormFile? PhotoFile { get; set; } // For file upload

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties (for display purposes in UI, not directly mapped to API)
        public string PositionTitle { get; set; } = string.Empty;
        public string ElectionTitle { get; set; } = string.Empty;
    }

    public class CandidateListResponse
    {
        public List<CandidateModel> Items { get; set; } = new List<CandidateModel>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class PhotoResponse
    {
        public bool HasPhoto { get; set; }
        public string StorageType { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public string FullUrl { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class CandidateWithVotesDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Biography { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public bool HasPhoto { get; set; }
        public int OrderPosition { get; set; }
        public bool IsActive { get; set; }
        public int PositionId { get; set; }
        public string PositionTitle { get; set; } = string.Empty;
        public int VotesCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

}