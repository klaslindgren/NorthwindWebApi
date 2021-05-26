using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Entities
{
    public class User : IdentityUser
    {
        public int EmployeeID { get; set; }
        public string Country { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public Token Token { get; set; }
        public RefreshToken RefreshToken { get; set; }
  
    }
}
