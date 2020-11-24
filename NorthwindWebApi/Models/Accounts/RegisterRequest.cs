using System.ComponentModel.DataAnnotations;

namespace NorthwindWebApi.Models.Accounts
{
    public class RegisterRequest
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }
        [Required]
        public object Country { get; set; }
    }
}