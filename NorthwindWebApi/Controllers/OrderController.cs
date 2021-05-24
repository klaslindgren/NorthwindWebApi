﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NorthwindWebApi.Data;
using NorthwindWebApi.Models;
using NorthwindWebApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace NorthwindWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IConfiguration _configuration;
        private readonly NorthwindContext northwindContext;
        private readonly IdentityContext identityContext;

        public OrderController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, NorthwindContext northwindContext, IdentityContext identityContext)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            this.identityContext = identityContext;
            this.northwindContext = northwindContext;
        }

        [Authorize]
        [HttpGet("GetMyOrders/{id?}")]
        public async Task<ActionResult<IEnumerable<Orders>>> GetMyOrders(string id = null)
        {
            var user = Request.HttpContext.User;
            var employee = await userManager.FindByNameAsync(user.Identity.Name);

            if (!(user.IsInRole("Admin") || user.IsInRole("Vd")))
                return await northwindContext.Orders.Where(e => e.EmployeeId == employee.EmployeeID).ToListAsync();

            if (string.IsNullOrEmpty(id))
                return await northwindContext.Orders.Where(e => e.EmployeeId == employee.EmployeeID).ToListAsync();

            if (user.IsInRole("Admin") || user.IsInRole("Vd"))
                return await northwindContext.Orders.Where(e => e.EmployeeId == Int32.Parse(id)).ToListAsync();


            return NotFound();

        }

        // GET: api/Orders/5
        [Authorize(Policy = "AboveEmployee")]
        [HttpGet("GetCountryOrders/{country}")]
        public async Task<ActionResult<IEnumerable<Orders>>> GetCountryOrders(string country)
        {
            var user = Request.HttpContext.User;
            var employee = await userManager.FindByNameAsync(user.Identity.Name);

            if (user.IsInRole("Admin") || user.IsInRole("Vd"))
                return await northwindContext.Orders.Where(c => c.ShipCountry == country).ToListAsync();

            else if (user.IsInRole("CountryManager"))
            {
                if (user.FindFirst("Country").ToString() != country)
                    return Unauthorized();
                
                return await northwindContext.Orders.Where(c => c.ShipCountry == employee.Country).ToListAsync();
            }
            return NotFound();
        }

        [Authorize(Policy = "AboveEmployee")]
        [HttpGet("GetAllOrders")]
        public async Task<ActionResult<IEnumerable<Orders>>> GetAllOrders()
        {
            var user = Request.HttpContext.User;
            var employee = await userManager.FindByNameAsync(user.Identity.Name);

            if (user.IsInRole("Admin") || user.IsInRole("Vd"))
                return await northwindContext.Orders.ToListAsync();

            else if (user.IsInRole("CountryManager"))
                return await northwindContext.Orders.Where(c => c.ShipCountry == employee.Country).ToListAsync();

            return NotFound();
        }

        // PUT: api/Orders/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOrders(int id, Orders orders)
        {
            if (id != orders.OrderId)
            {
                return BadRequest();
            }

            northwindContext.Entry(orders).State = EntityState.Modified;

            try
            {
                await northwindContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrdersExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Orders
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<Orders>> PostOrders(Orders orders)
        {
            northwindContext.Orders.Add(orders);
            await northwindContext.SaveChangesAsync();

            return CreatedAtAction("GetOrders", new { id = orders.OrderId }, orders);
        }

        // DELETE: api/Orders/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Orders>> DeleteOrders(int id)
        {
            var orders = await northwindContext.Orders.FindAsync(id);
            if (orders == null)
            {
                return NotFound();
            }

            northwindContext.Orders.Remove(orders);
            await northwindContext.SaveChangesAsync();

            return orders;
        }

        private bool OrdersExists(int id)
        {
            return northwindContext.Orders.Any(e => e.OrderId == id);
        }
    }
}