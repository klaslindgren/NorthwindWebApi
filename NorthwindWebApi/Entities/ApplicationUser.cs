using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public int EmployeeID { get; set; }
    }
}
