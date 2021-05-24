using Microsoft.AspNetCore.Mvc;
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

            //Check if refreshtoken is active
            var refreshToken = user.RefreshTokens.LastOrDefault();
            if (refreshToken == null)
                refreshToken = generateRefreshToken();
            else if (refreshToken.IsExpired)
                refreshToken = generateRefreshToken();

            // authentication successful, generate jwt token
            var jwtToken = await generateJwtToken(user);


            // save refresh token and jwtToken
            user.RefreshTokens.Add(refreshToken);
            user.AccessToken = jwtToken;
            await userManager.UpdateAsync(user);  
            return Ok(new Response { Message = "Login Successful", AccessToken = jwtToken, RefreshToken = refreshToken.Token });
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

            //   Get EmployeeID from northwind
            var employee = northwindContext.Employees.Where(e => e.FirstName == model.FirstName && e.LastName == model.LastName).FirstOrDefault();
            

            //  Create new user in identity
            user = new User()
            {
                UserName = model.UserName,
                FirstName = model.FirstName,
                LastName = model.LastName,
                SecurityStamp = Guid.NewGuid().ToString(),
                EmployeeID = employee.EmployeeId,
                Country = employee.Country
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });

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

                if (updateRequest.UserName != null)
                    user.UserName = updateRequest.UserName;
                if (updateRequest.FirstName != null)
                    user.FirstName = updateRequest.FirstName;
                if (updateRequest.LastName != null)
                    user.LastName = updateRequest.LastName;
                if (updateRequest.Country != null)
                    user.Country = updateRequest.Country;
                await userManager.UpdateAsync(user);
                if (updateRequest.Role != null)
                {
                    var addRole = RoleExists(updateRequest.Role);

                    if (addRole == null)
                        return BadRequest(new Response { Message = "Role does not exist. Avalible roles are: Vd, CountryManager" });

                    await userManager.AddToRoleAsync(user, addRole);
                }
                await userManager.ChangePasswordAsync(user, user.PasswordHash, updateRequest.Password);

                return Ok(new Response { Message = "Information updated successfully" });
            }

            if (requestUser.IsInRole(Roles.Employee))
            {
                if(updateRequest.UserName != null)
                    user.UserName = updateRequest.UserName;
                if (updateRequest.FirstName != null)
                { 
                    user.FirstName = updateRequest.FirstName;
                    employee.FirstName = updateRequest.FirstName;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                if (updateRequest.LastName != null) 
                {
                    user.LastName = updateRequest.LastName;
                    employee.LastName = updateRequest.LastName;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                if (updateRequest.Country != null)
                {
                    user.Country = updateRequest.Country;
                    employee.Country = updateRequest.Country;
                    northwindContext.Update(employee);
                    await northwindContext.SaveChangesAsync();
                }

                await userManager.UpdateAsync(user);
                await userManager.ChangePasswordAsync(user, user.PasswordHash, updateRequest.Password);

                return Ok(new Response { Message = "Information updated successfully" });
            }

            return NotFound(new Response { Message = "Could not find any users" });
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost("DeleteUser")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteRequest deleteRequest)
        {
            var employee = await userManager.FindByNameAsync(deleteRequest.UserName);
            if(employee != null)
            {
                await userManager.DeleteAsync(employee);
                return Ok(new Response { Message = "User deleted succesfully" });
            }

            return BadRequest(new Response { Message = "User not found"});
        }

        [Authorize]
        [HttpPost("refreshtoken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest refreshToken)
        {
            var requestUser = Request.HttpContext.User;
            var user = await userManager.FindByNameAsync(requestUser.Identity.Name);

            var latestRefreshToken = user.RefreshTokens.OrderByDescending(t => t.Created).FirstOrDefault();

            
            if(!latestRefreshToken.IsExpired && refreshToken.RefreshToken == latestRefreshToken.Token)
            {
                var jwtToken = await generateJwtToken(user);

                user.AccessToken = jwtToken;
                await userManager.UpdateAsync(user);
                return Ok(new Response { Message = "New Access Token Created", AccessToken = jwtToken});
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

        private async Task<string> generateJwtToken(User user)
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
