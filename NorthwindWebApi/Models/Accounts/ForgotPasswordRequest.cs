using System.ComponentModel.DataAnnotations;

namespace NorthwindWebApi.Models.Accounts
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}