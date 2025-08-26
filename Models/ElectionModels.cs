using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElectionAdminPanel.Web.Models
{
    public class ElectionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O título é obrigatório.")]
        [StringLength(255, ErrorMessage = "O título não pode exceder 255 caracteres.")]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Display(Name = "Tipo de Eleição")]
        [StringLength(50, ErrorMessage = "O tipo de eleição não pode exceder 50 caracteres.")]
        public string ElectionType { get; set; } = "internal";

        public string Status { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de início é obrigatória.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Data de Início")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "A data de término é obrigatória.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Data de Término")]
        public DateTime EndDate { get; set; }

        [StringLength(100, ErrorMessage = "O fuso horário não pode exceder 100 caracteres.")]
        [Display(Name = "Fuso Horário")]
        public string Timezone { get; set; } = "America/Sao_Paulo";

        [Display(Name = "Permitir Votos em Branco")]
        public bool AllowBlankVotes { get; set; }

        [Display(Name = "Permitir Votos Nulos")]
        public bool AllowNullVotes { get; set; }

        [Display(Name = "Exigir Justificativa")]
        public bool RequireJustification { get; set; }

        [Display(Name = "Máximo de Votos por Eleitor")]
        public int MaxVotesPerVoter { get; set; } = 1;

        [StringLength(20, ErrorMessage = "O método de votação não pode exceder 20 caracteres.")]
        [Display(Name = "Método de Votação")]
        public string VotingMethod { get; set; } = "single_choice";

        [StringLength(20, ErrorMessage = "A visibilidade dos resultados não pode exceder 20 caracteres.")]
        [Display(Name = "Visibilidade dos Resultados")]
        public string ResultsVisibility { get; set; } = "after_election";

        public int CreatedBy { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Required(ErrorMessage = "A empresa é obrigatória.")]
        [Display(Name = "Empresa")]
        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = string.Empty;
        public string CompanyCnpj { get; set; } = string.Empty;
    }

    public class ElectionListResponse
    {
        public List<ElectionModel> Items { get; set; } = new List<ElectionModel>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public object? Errors { get; set; }
    }

    public class SealElectionRequest
    {
        public int ElectionId { get; set; }
        public SealConfirmedChecks ConfirmedChecks { get; set; } = new SealConfirmedChecks();
    }

    public class SealConfirmedChecks
    {
        public bool VerifyInformation { get; set; }
        public bool UnderstandIrreversible { get; set; }
        public bool HaveBackup { get; set; }
    }

    public class SealedElectionsStatus
    {
        public bool HasSealedElections { get; set; }
        public List<int> SealedElectionIds { get; set; } = new List<int>();
    }

    public class ElectionForSealingDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ElectionSealStatusDto
    {
        public bool IsSealed { get; set; }
        public string SealHash { get; set; } = string.Empty;
        public DateTime? SealedAt { get; set; }
        public string SealedBy { get; set; } = string.Empty;
    }
}