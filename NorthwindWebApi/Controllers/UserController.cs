using AutoMapper;
using Microsoft.AspNetCore.Http;
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

namespace NorthwindWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public UserController (IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            var response = await _accountService.AuthenticateAsync(model);
            return Ok(response);
        }

        [HttpPost("register")]
        public IActionResult Register(RegisterRequest model)
        {
            _accountService.Register(model, Request.Headers["origin"]);
            return Ok(new { message = "Registration successful" });
        }

        [HttpGet]
        public ActionResult<IEnumerable<User>> GetAll()
        {
            var accounts = _accountService.GetAll();
            return Ok(accounts);
        }

    }
}
