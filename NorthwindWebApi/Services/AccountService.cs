using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using NorthwindWebApi.Entities;
using NorthwindWebApi.Models.Accounts;
using NorthwindWebApi.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using NorthwindWebApi.Models.Account;
using NorthwindWebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NorthwindWebApi.Services
{
    public interface IAccountService
    {
        Task<Response> AuthenticateAsync(LoginModel model);
        Task<Response> Register(RegisterRequest model, string origin);
        IEnumerable<User> GetAll();
        User GetById(int id);
    }

    public class AccountService : IAccountService
    {
        private readonly NorthwindContext northwindContext;
        private readonly IdentityContext identityContext;
        private readonly IConfiguration _configuration;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountService(NorthwindContext nContext, IdentityContext iContext, IConfiguration configuration, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            northwindContext = nContext;
            identityContext = iContext;
            _configuration = configuration;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<Response> AuthenticateAsync(LoginModel model)
        {
            var user = _userManager.Users.Where(u => u.UserName == model.UserName).FirstOrDefault();

            bool validPass = await _userManager.CheckPasswordAsync(user, model.Password);

            if (model == null || !validPass)
                throw new Exception("Username or password is incorrect");

            //Check if refreshtoken is active
            var refreshToken = user.RefreshTokens.LastOrDefault();
            if (refreshToken == null)
                refreshToken = generateRefreshToken();
            else if (refreshToken.IsExpired)
                refreshToken = generateRefreshToken();

            // authentication successful so generate jwt token
            var jwtToken = await generateJwtToken(user);


            // save refresh token and jwtToken
            user.RefreshTokens.Add(refreshToken);
            user.AccessToken = jwtToken;
            await _userManager.UpdateAsync(user);
            return new Response { Token = jwtToken, RefreshToken = refreshToken };
        }

        public async Task<Response> Register(RegisterRequest model, string origin)
        {
            User user;

            if (_roleManager.Roles.Count() == 0)                 //      Create Roles
            {
                await _roleManager.CreateAsync(new IdentityRole(Roles.Employee));
                await _roleManager.CreateAsync(new IdentityRole(Roles.VD));
                await _roleManager.CreateAsync(new IdentityRole(Roles.Admin));
                await _roleManager.CreateAsync(new IdentityRole(Roles.CountryManager));
            }

            var userExists = await _userManager.FindByEmailAsync(model.Email);               //      Check if email already registered
            if (userExists != null)
                return  new Response { Status = "Error", Message = "User already exists!" };

            userExists = await _userManager.FindByNameAsync(model.UserName);               //      Check if username already registered
            if (userExists != null)
                return new Response { Status = "Error", Message = "User already exists!" };

            //var query = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault();         //      Check if user already exists in Northwind
            //if (query != null)
            //{
            //    user = new User()
            //    {
            //        UserName = model.UserName,
            //        Email = model.Email,
            //        SecurityStamp = Guid.NewGuid().ToString(),
            //        EmployeeID = query.EmployeeId,
            //        PasswordHash = model.Password
            //    };
            //}

            //using (SqlConnection connection = new SqlConnection(northwindContext.Database.GetDbConnection().ConnectionString))          //      Add user to Northwind
            //{
            //    connection.Open();
            //    SqlCommand command = new SqlCommand("INSERT INTO Employees (LastName, FirstName, Country) VALUES (@LastName, @FirstName, @Country)", connection);
            //    command.Parameters.AddWithValue("@LastName", model.LastName);
            //    command.Parameters.AddWithValue("@FirstName", model.FirstName);
            //    command.Parameters.AddWithValue("@Country", model.Country);
            //    command.ExecuteNonQuery();
            //}


            //query = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault();
            user = new User()
            {
                UserName = model.UserName,
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                PasswordHash = model.Password,
                //EmployeeID = query.EmployeeId
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." };

            if (identityContext.Users.Count() == 1)                         //  First user created gets Admin-role
                await _userManager.AddToRoleAsync(user, Roles.Admin);

            await _userManager.AddToRoleAsync(user, Roles.Employee);     //  All other users gets Employee-role, Further roles can be added by admin

            return new Response { Status = "Success", Message = "Potato created successfully!" };

            //// validate
            //if (identityContext.Accounts.Any(x => x.Email == model.Email))
            //{
            //    // send already registered error in email to prevent account enumeration
            //    throw new Exception("Account already exists");
            //    //return;
            //}

            //// map model to new account object
            //var account = _mapper.Map<User>(model);

            //// first registered account is an admin
            //var isFirstAccount = identityContext.Accounts.Count() == 0;
            //account.Role = isFirstAccount ? Roles.Admin : Roles.Employee;
            //account.Created = DateTime.UtcNow;
            //account.JwtToken = randomTokenString();

            //// hash password
            //account.PasswordHash = model.Password;

            //// save account
            //identityContext.Accounts.Add(account);
            //identityContext.SaveChanges();

        }

        public IEnumerable<User> GetAll()
        {
            var accounts = _userManager.Users.ToList();
            return accounts;
        }

        public User GetById(int id)
        {
            var account = getAccount(id);
            return account;
        }

        // helper methods

        private User getAccount(int id)
        {
            var account = identityContext.User.Find(id);
            if (account == null) throw new KeyNotFoundException("Account not found");
            return account;
        }

        private (RefreshToken, User) getRefreshToken(string token)
        {
            var account = identityContext.User.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token));
            if (account == null) throw new Exception("Invalid token");
            var refreshToken = account.RefreshTokens.Single(x => x.Token == token);
            if (refreshToken.IsExpired) throw new Exception("Invalid token");
            return (refreshToken, account);
        }

        private async Task<string> generateJwtToken(User user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);

            //var country = northwindContext.Employees.Find(user.EmployeeID).Country.ToString();
            var country = "sweden";

            var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Country, country)
                };

            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.UtcNow.AddMinutes(30),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private RefreshToken generateRefreshToken()
        {
            return new RefreshToken
            {
                Token = randomTokenString(),
                Expires = DateTime.UtcNow.AddDays(5),
                Created = DateTime.UtcNow,
            };
        }

        private string randomTokenString()
        {
            using var rngCryptoServiceProvider = new RNGCryptoServiceProvider();
            var randomBytes = new byte[40];
            rngCryptoServiceProvider.GetBytes(randomBytes);
            // convert random bytes to hex string
            return BitConverter.ToString(randomBytes).Replace("-", "");
        }
    }
}
