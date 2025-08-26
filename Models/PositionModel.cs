using System;
using System.ComponentModel.DataAnnotations;

namespace ElectionAdminPanel.Web.Models
{
    public class PositionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O título do cargo é obrigatório.")]
        [StringLength(255, ErrorMessage = "O título não pode exceder 255 caracteres.")]
        [Display(Name = "Título do Cargo")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Descrição")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "ID da Eleição")]
        public int ElectionId { get; set; }

        [Display(Name = "Máximo de Candidatos")]
        public int MaxCandidates { get; set; }

        [Display(Name = "Máximo de Votos por Eleitor")]
        public int MaxVotesPerVoter { get; set; }

        [Display(Name = "Posição da Ordem")]
        public int OrderPosition { get; set; }

        [Display(Name = "Ativo")]
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PositionListResponse
    {
        public List<PositionModel> Items { get; set; } = new List<PositionModel>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
