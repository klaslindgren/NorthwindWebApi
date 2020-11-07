using System;
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
    public class OrdersController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IConfiguration _configuration;
        private readonly NorthwindContext northwindContext;
        private readonly IdentityContext identityContext;

        public OrdersController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, NorthwindContext northwindContext, IdentityContext identityContext)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            this.identityContext = identityContext;
            this.northwindContext = northwindContext;
        }

        //[Authorize]
        [HttpGet("GetMyOrders/{id}")]
        public async Task<ActionResult<IEnumerable<Orders>>> GetMyOrders(int id)
        {
            var user = identityContext.User.Where(u => u.EmployeeID == id).FirstOrDefault();
            var roles = await userManager.GetRolesAsync(user);
            var orders = "potato";

            if (roles.Count() == 1)
            {
                return Ok(await northwindContext.Orders.Where(o => o.EmployeeId == id).ToListAsync());
            }

            if (orders == null)
            {
                return NotFound();
            }

            return Ok(orders);
        }

        // GET: api/Orders/5
        //[Authorize(Policy = "AboveEmployee")]

        [HttpGet("GetCountryOrders/{country}")]
        public async Task<ActionResult<IEnumerable<Orders>>> GetCountryOrders(string country)
        {

            // if(   CountryManager får bara se sina ordrar   eg.   User.Country == country)

            // if(  Admin || Vd Får se alla ordrar för valt country  )

            var orders = await northwindContext.Orders.Where(o => o.ShipCountry == country).ToListAsync();

            if (orders == null)
            {
                return NotFound();
            }

            return Ok(orders);
        }

        //[Authorize(Policy = "AboveEmployee")]
        [HttpGet("GetAllOrders/{id}")]
        public async Task<ActionResult<IEnumerable<Orders>>> GetAllOrders(int id)
        {
            var orders = await northwindContext.Orders.Where(o => o.EmployeeId == id).ToListAsync();

            if (orders == null)
            {
                return NotFound();
            }

            return Ok(orders);
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
