using System.ComponentModel.DataAnnotations;

namespace NorthwindWebApi.Models.Accounts
{
    public class ValidateResetTokenRequest
    {
        [Required]
        public string Token { get; set; }
    }
}