﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using NorthwindWebApi.Entities;
using NorthwindWebApi.Models.Accounts;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using NorthwindWebApi.Models.Account;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NorthwindWebApi.Data;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;

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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            //  Check if user exists with valid password
            var user = userManager.Users.Where(u => u.UserName == model.UserName).FirstOrDefault();
            bool validPass = await userManager.CheckPasswordAsync(user, model.Password);

            if (user == null || !validPass)
                return BadRequest(new Response { Message = "Username or password is incorrect" });

            // Generate refresh token
            var refreshToken = generateRefreshToken();

            // generate jwt token
            var jwtToken = await generateJwtToken(user);


            // save refresh token and jwtToken
            user.RefreshToken = refreshToken;
            user.Token = jwtToken;
            await userManager.UpdateAsync(user);  
            return Ok(new Response { Message = "Login Successful", Token = jwtToken, RefreshToken = refreshToken });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody]RegisterRequest model)
        {
            User user;

            //  Create Roles
            if (roleManager.Roles.Count() == 0)         
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Employee));
                await roleManager.CreateAsync(new IdentityRole(Roles.VD));
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
                await roleManager.CreateAsync(new IdentityRole(Roles.CountryManager));
            }


            //  Check if username already registered
            var userNameExists = await userManager.FindByNameAsync(model.UserName);         
            if (userNameExists != null)
                return BadRequest(new Response { Status = "Error", Message = "Username already exists, try another username!" });

            //  Create new user in identity
            user = new User()
            {
                UserName = model.UserName,
                FirstName = model.FirstName,
                LastName = model.LastName,
                SecurityStamp = Guid.NewGuid().ToString(),
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });

            //  Check if user already exists in Northwind
            var employeeExists = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault();

            //      Add user to Northwind
            if (employeeExists == null)
            {
                using (SqlConnection connection = new SqlConnection(northwindContext.Database.GetDbConnection().ConnectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("INSERT INTO Employees (LastName, FirstName, Country) VALUES (@LastName, @FirstName, @Country)", connection);
                    command.Parameters.AddWithValue("@LastName", model.LastName.ToString());
                    command.Parameters.AddWithValue("@FirstName", model.FirstName.ToString());
                    command.Parameters.AddWithValue("@Country", model.Country.ToString());
                    command.ExecuteNonQuery();
                }
            }

            //   Get EmployeeID and country from northwind
            var employee = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault();
            user.EmployeeID = employee.EmployeeId;
            user.Country = employee.Country;
            await userManager.UpdateAsync(user);

            //  First user created gets Admin-role
            if (identityContext.Users.Count() == 1)                         
                await userManager.AddToRoleAsync(user, Roles.Admin);

            //  All other users gets Employee-role, Further roles can be added by admin
            await userManager.AddToRoleAsync(user, Roles.Employee);     

            return Ok(new Response { Status = "Success", Message = "Potato created successfully!" });

        }

        [Authorize(Policy = "AdminVd")]
        [HttpGet("GetUsers")]
        public async Task<ActionResult<IEnumerable<UserModel>>> GetUsers()
        {
            var userList = await userManager.Users.ToListAsync();
            List<UserModel> result = new List<UserModel>();

            if (userList != null)
            {
                foreach (var item in userList)
                {
                    result.Add(new UserModel { UserName = item.UserName, FirstName = item.FirstName, LastName = item.LastName, Country = item.Country });
                }
                return Ok(result);
            }

            return NotFound(new Response { Message = "Could not find any users"});
        }

        [Authorize]
        [HttpPut("UpdateUser/{userName?}")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateRequest updateRequest, string userName)
        {
            var requestUser = Request.HttpContext.User;
            var user = await userManager.FindByNameAsync(requestUser.Identity.Name);
            var employee = northwindContext.Employees.Where(e => e.EmployeeId == user.EmployeeID).FirstOrDefault();
            var updateUserAsAdmin = userManager.Users.Where(u => u.UserName == userName).FirstOrDefault();

            if (updateUserAsAdmin == null && userName != null)
                return BadRequest(new Response { Message = "Could not find user with that username" });

            if (!requestUser.IsInRole("Admin") && userName != null)
            {
                return Unauthorized(new Response { Message = "Only Admin can update other users" });
            }

            if (requestUser.IsInRole("Admin") && userName != null)
            {
                user = await userManager.FindByNameAsync(userName);

                if (updateRequest.UserName != string.Empty)
                    user.UserName = updateRequest.UserName;
                if (updateRequest.FirstName != string.Empty)
                {
                    user.FirstName = updateRequest.FirstName;
                    employee.FirstName = updateRequest.FirstName;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                if (updateRequest.LastName != string.Empty)
                {
                    user.LastName = updateRequest.LastName;
                    employee.LastName = updateRequest.LastName;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                if (updateRequest.Country != string.Empty)
                {
                    user.Country = updateRequest.Country;
                    employee.Country = updateRequest.Country;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }
                await userManager.UpdateAsync(user);

                if (updateRequest.Role != string.Empty)
                {
                    var addRole = RoleExists(updateRequest.Role);

                    if (addRole == null)
                        return BadRequest(new Response { Message = "Role does not exist. Avalible roles are: Vd, CountryManager" });

                    await userManager.AddToRoleAsync(user, addRole);
                }
                if (updateRequest.Password != string.Empty)
                    await userManager.ChangePasswordAsync(user, user.PasswordHash, updateRequest.Password);

                return Ok(new Response { Message = "Information updated successfully" });
            }

            if (requestUser.IsInRole(Roles.Employee))
            {
                if(updateRequest.UserName != string.Empty)
                    user.UserName = updateRequest.UserName;
                if (updateRequest.FirstName != string.Empty)
                { 
                    user.FirstName = updateRequest.FirstName;
                    employee.FirstName = updateRequest.FirstName;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                if (updateRequest.LastName != string.Empty) 
                {
                    user.LastName = updateRequest.LastName;
                    employee.LastName = updateRequest.LastName;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                if (updateRequest.Country != string.Empty)
                {
                    user.Country = updateRequest.Country;
                    employee.Country = updateRequest.Country;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                await userManager.UpdateAsync(user);
                if (updateRequest.Password != string.Empty)
                    await userManager.ChangePasswordAsync(user, user.PasswordHash, updateRequest.Password);


                return Ok(new Response { Message = "Information updated successfully" });
            }

            return NotFound(new Response { Message = "Could not find any users" });
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost("DeleteUser")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteRequest deleteRequest)
        {
            var user = await userManager.FindByNameAsync(deleteRequest.UserName);
            if(user != null)
            {
                var employee = northwindContext.Employees.Where(e => e.EmployeeId == user.EmployeeID).FirstOrDefault();
                northwindContext.Remove(employee);
                await northwindContext.SaveChangesAsync();
                await userManager.DeleteAsync(user);
                return Ok(new Response { Message = "User deleted succesfully" });
            }

            return BadRequest(new Response { Message = "User not found"});
        }

        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            //  get user if its the lastest refreshtoken
            var user = await identityContext.User.Where(r => r.RefreshToken.Token == request.RefreshToken).Include(r => r.RefreshToken).FirstOrDefaultAsync();

            //  return if refreshtoken not active
            if (user == null)
                return BadRequest(new Response { Message = "RefreshToken is not valid. Please Log in Again" });


            if (!user.RefreshToken.IsExpired)
            {
                var jwtToken = await generateJwtToken(user);
                var refreshToken = generateRefreshToken();
                user.Token = jwtToken;
                user.RefreshToken = refreshToken;
                await userManager.UpdateAsync(user);
                return Ok(new Response { Message = "New Access and refreshtoken Created", Token = jwtToken, RefreshToken = refreshToken});
            }

            return BadRequest(new Response { Message = "RefreshToken is not valid. Please Log in Again" });
        }


        // HELPER METHODS 

        private string RoleExists(string role)
        {
            if (role == Roles.VD)
                return role;
            if (role == Roles.CountryManager)
                return role;
            return null;
        }

        private async Task<Token> generateJwtToken(User user)
        {
            var userRoles = await userManager.GetRolesAsync(user);

            var country = northwindContext.Employees.Find(user.EmployeeID).Country.ToString();

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

            var createToken = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.UtcNow.AddMinutes(30),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            Token token = new Token()
            {
                Payload = new JwtSecurityTokenHandler().WriteToken(createToken),
                Expires = DateTime.UtcNow.AddMinutes(30)
            };

            return token;
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
