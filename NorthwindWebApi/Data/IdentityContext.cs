using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NorthwindWebApi.Entities;
using NorthwindWebApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Data
{
    public class IdentityContext : IdentityDbContext<Account>
    {
        public DbSet<Account> Accounts { get; set; }
        public IdentityContext(DbContextOptions<IdentityContext> options) : base(options)
        { 
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }
    }
}
