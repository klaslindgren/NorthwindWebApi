using System.ComponentModel.DataAnnotations;

namespace NorthwindWebApi.Models.Accounts
{
    public class VerifyEmailRequest
    {
        [Required]
        public string Token { get; set; }
    }
}