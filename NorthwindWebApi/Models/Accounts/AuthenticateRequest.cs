using System.ComponentModel.DataAnnotations;

namespace NorthwindWebApi.Models.Accounts
{
    public class AuthenticateRequest
    {
        [Required]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}