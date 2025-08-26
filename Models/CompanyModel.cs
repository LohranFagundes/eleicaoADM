using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElectionAdminPanel.Web.Models
{
    public class CompanyModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome fantasia é obrigatório.")]
        [StringLength(255, ErrorMessage = "O nome fantasia não pode exceder 255 caracteres.")]
        [Display(Name = "Nome Fantasia")]
        public string NomeFantasia { get; set; } = string.Empty;

        [Required(ErrorMessage = "A razão social é obrigatória.")]
        [StringLength(255, ErrorMessage = "A razão social não pode exceder 255 caracteres.")]
        [Display(Name = "Razão Social")]
        public string RazaoSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CNPJ é obrigatório.")]
        [StringLength(18, ErrorMessage = "O CNPJ não pode exceder 18 caracteres.")]
        [Display(Name = "CNPJ")]
        public string Cnpj { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CEP é obrigatório.")]
        [StringLength(10, ErrorMessage = "O CEP não pode exceder 10 caracteres.")]
        [Display(Name = "CEP")]
        public string Cep { get; set; } = string.Empty;

        [Required(ErrorMessage = "O bairro é obrigatório.")]
        [StringLength(100, ErrorMessage = "O bairro não pode exceder 100 caracteres.")]
        [Display(Name = "Bairro")]
        public string Bairro { get; set; } = string.Empty;

        [Required(ErrorMessage = "O logradouro é obrigatório.")]
        [StringLength(255, ErrorMessage = "O logradouro não pode exceder 255 caracteres.")]
        [Display(Name = "Logradouro")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "O número é obrigatório.")]
        [StringLength(10, ErrorMessage = "O número não pode exceder 10 caracteres.")]
        [Display(Name = "Número")]
        public string Numero { get; set; } = string.Empty;

        [Required(ErrorMessage = "A cidade é obrigatória.")]
        [StringLength(100, ErrorMessage = "A cidade não pode exceder 100 caracteres.")]
        [Display(Name = "Cidade")]
        public string Cidade { get; set; } = string.Empty;

        [StringLength(2, ErrorMessage = "O estado não pode exceder 2 caracteres.")]
        [Display(Name = "Estado")]
        public string Estado { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Telefone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Ativo")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "URL do Logo")]
        public string? LogoUrl { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CompanyListResponse
    {
        public List<CompanyModel> Items { get; set; } = new List<CompanyModel>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}