using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElectionAdminPanel.Web.Models
{
    public class VoterModel
    {
        public int Id { get; set; }

        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "CPF")]
        public string Cpf { get; set; } = string.Empty;

        [Display(Name = "Peso do Voto")]
        public double? VoteWeight { get; set; }

        [Display(Name = "Ativo")]
        public bool IsActive { get; set; }

        [Display(Name = "Verificado")]
        public bool IsVerified { get; set; }

        // Assuming phone might be part of the model, even if not directly editable via API for now
        [Display(Name = "Telefone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Senha")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Token de Validação")]
        public string ValidationToken { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class VoterListResponse
    {
        public List<VoterModel> Items { get; set; } = new List<VoterModel>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}