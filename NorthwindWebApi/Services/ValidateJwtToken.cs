using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NorthwindWebApi.Services
{
    public class ValidateJwtToken
    {
        private readonly IConfiguration _configuration;
        public ValidateJwtToken(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool ValidateToken(string token) 
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
            var myIssuer = _configuration["JWT:ValidIssuer"];
            var myAudience = _configuration["JWT:ValidAudience"];

            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = myIssuer,
                ValidAudience = myAudience,
                IssuerSigningKey = securityKey
            }, out SecurityToken validatedToken);

            return true;
        }   
    }
}
