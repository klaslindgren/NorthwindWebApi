using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Models.Accounts
{
    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }
}
