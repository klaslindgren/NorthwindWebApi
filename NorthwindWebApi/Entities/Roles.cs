using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Entities 
{
    static class Roles
    {
        public static readonly string Employee = "Employee";
        public static readonly string VD = "Vd";
        public static readonly string CountryManager = "CountryManager";
        public static readonly string Admin = "Admin";
    }
}