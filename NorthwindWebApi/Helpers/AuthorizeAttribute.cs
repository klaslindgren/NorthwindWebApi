//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using NorthwindWebApi.Entities;
//using Microsoft.AspNetCore.Identity;

//[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
//public class AuthorizeAttribute : Attribute, IAuthorizationFilter
//{
//    private readonly IList<IdentityRole> _roles;

//    public AuthorizeAttribute(params Roles[] roles)
//    {
//        _roles = roles ?? new Roles[] { };
//    }

//    public void OnAuthorization(AuthorizationFilterContext context)
//    {
//        var account = (Account)context.HttpContext.Items["Account"];
//        if (account == null || (_roles.Any() && !_roles.Contains(account.Role)))
//        {
//            // not logged in or role not authorized
//            context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
//        }
//    }
//}