using System.ComponentModel.DataAnnotations;
using NorthwindWebApi.Entities;

namespace NorthwindWebApi.Models.Accounts
{
    public class UpdateRequest
    {
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
        [Compare("Password")]
        public string ConfirmPassword { get; set; }
        public string Country { get; set; }
        public string Role { get; set; }
    }
}