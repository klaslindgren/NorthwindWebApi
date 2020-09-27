using System;
using System.Text.Json.Serialization;

namespace NorthwindWebApi.Models.Accounts
{
    public class AuthenticateResponse
    {
        public int EmployeeID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Updated { get; set; }
        public bool IsVerified { get; set; }
        public string JwtToken { get; set; }

        public string RefreshToken { get; set; }
    }
}