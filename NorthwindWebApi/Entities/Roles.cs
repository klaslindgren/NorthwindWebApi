using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Entities 
{
    public class Roles
    {
        public const string Employee = "Employee";
        public const string VD = "Vd";
        public const string CountryManager = "CountryManager";
        public const string Admin = "Admin";
    }
}