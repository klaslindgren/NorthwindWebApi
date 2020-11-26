﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using NorthwindWebApi.Entities;
using NorthwindWebApi.Models.Accounts;
using NorthwindWebApi.Services;
using System.Linq;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using NorthwindWebApi.Models.Account;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NorthwindWebApi.Data;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Data.SqlClient;

namespace NorthwindWebApi.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IConfiguration _configuration;
        private readonly NorthwindContext northwindContext;
        private readonly IdentityContext identityContext;

        public UserController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, NorthwindContext northwindContext, IdentityContext identityContext)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            this.identityContext = identityContext;
            this.northwindContext = northwindContext;
        }

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody]LoginModel model)
        //{
        //    var user = userManager.Users.Where(u => u.UserName == model.UserName).FirstOrDefault();

        //    bool validPass = await userManager.CheckPasswordAsync(user, model.Password);

        //    if (model == null || !validPass)
        //        throw new Exception("Username or password is incorrect");

        //    //Check if refreshtoken is active
        //    var refreshToken = user.RefreshTokens.LastOrDefault();
        //    if (refreshToken == null)
        //        refreshToken = generateRefreshToken();
        //    else if (refreshToken.IsExpired)
        //        refreshToken = generateRefreshToken();

        //    // authentication successful so generate jwt token
        //    var jwtToken = await generateJwtToken(user);


        //    // save refresh token and jwtToken
        //    user.RefreshTokens.Add(refreshToken);
        //    user.AccessToken = jwtToken;
        //    await userManager.UpdateAsync(user);
        //    return Ok(new Response { Token = jwtToken, RefreshToken = refreshToken });
        //}

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody]RegisterRequest model)
        {
            User user;

            if (roleManager.Roles.Count() == 0)         //  Create Roles
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Employee));
                await roleManager.CreateAsync(new IdentityRole(Roles.VD));
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
                await roleManager.CreateAsync(new IdentityRole(Roles.CountryManager));
            }

            var emailExists = await userManager.FindByEmailAsync(model.Email);          //  Check if email already registered
            if (emailExists != null)
                return BadRequest(new Response { Status = "Error", Message = "Email already registered!" });

            var userNameExists = await userManager.FindByNameAsync(model.UserName);         //  Check if username already registered
            if (userNameExists != null)
                return BadRequest(new Response { Status = "Error", Message = "Username already exists, try another username!" });

            var employeeExists = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault(); //  Check if user already exists in Northwind
            if (employeeExists == null)
            {
                using (SqlConnection connection = new SqlConnection(northwindContext.Database.GetDbConnection().ConnectionString))          //      Add user to Northwind
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("INSERT INTO Employees (LastName, FirstName, Country) VALUES (@LastName, @FirstName, @Country)", connection);
                    command.Parameters.AddWithValue("@LastName", model.LastName.ToString());
                    command.Parameters.AddWithValue("@FirstName", model.FirstName.ToString());
                    command.Parameters.AddWithValue("@Country", model.Country.ToString());
                    command.ExecuteNonQuery();
                }
            }

            var employee = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault();
            
            user = new User()
            {
                UserName = model.UserName,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                SecurityStamp = Guid.NewGuid().ToString(),
                EmployeeID = employee.EmployeeId,
                Country = employee.Country
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });

            if (identityContext.Users.Count() == 1)                         //  First user created gets Admin-role
                await userManager.AddToRoleAsync(user, Roles.Admin);

            await userManager.AddToRoleAsync(user, Roles.Employee);     //  All other users gets Employee-role, Further roles can be added by admin

            return Ok(new Response { Status = "Success", Message = "Potato created successfully!" });

        }

        //[Authorize(Policy = "AboveEmployee")]
        //[HttpGet("GetAllUsers")]
        //public async Task<ActionResult<IEnumerable<UserModel>>> GetAllUsers()
        //{
        //    var user = Request.HttpContext.User;
        //    var employee = await userManager.GetUserAsync(user);

        //    if (user.IsInRole("Admin") || user.IsInRole("Vd"))
        //        return await northwindContext.Employees.Where(e => e.EmployeeId == employee.EmployeeID).ToListAsync();

        //    else if (user.IsInRole("CountryManager"))
        //        return await northwindContext.Orders.Where(c => c.ShipCountry == employee.Country).ToListAsync();

        //    return NotFound();
        //}

        //private async Task<string> generateJwtToken(User user)
        //{
        //    var userRoles = await _userManager.GetRolesAsync(user);

        //    //var country = northwindContext.Employees.Find(user.EmployeeID).Country.ToString();
        //    var country = "sweden";

        //    var authClaims = new List<Claim>
        //        {
        //            new Claim(ClaimTypes.Name, user.UserName),
        //            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        //            new Claim(ClaimTypes.Country, country)
        //        };

        //    foreach (var userRole in userRoles)
        //    {
        //        authClaims.Add(new Claim(ClaimTypes.Role, userRole));
        //    }

        //    var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

        //    var token = new JwtSecurityToken(
        //        issuer: _configuration["JWT:ValidIssuer"],
        //        audience: _configuration["JWT:ValidAudience"],
        //        expires: DateTime.UtcNow.AddMinutes(30),
        //        claims: authClaims,
        //        signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        //        );

        //    return new JwtSecurityTokenHandler().WriteToken(token);
        //}

        //private RefreshToken generateRefreshToken()
        //{
        //    return new RefreshToken
        //    {
        //        Token = randomTokenString(),
        //        Expires = DateTime.UtcNow.AddDays(5),
        //        Created = DateTime.UtcNow,
        //    };
        //}

        //private string randomTokenString()
        //{
        //    using var rngCryptoServiceProvider = new RNGCryptoServiceProvider();
        //    var randomBytes = new byte[40];
        //    rngCryptoServiceProvider.GetBytes(randomBytes);
        //    // convert random bytes to hex string
        //    return BitConverter.ToString(randomBytes).Replace("-", "");
        //}
    }
}
