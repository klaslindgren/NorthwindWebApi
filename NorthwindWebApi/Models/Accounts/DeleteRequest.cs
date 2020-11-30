using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace NorthwindWebApi.Models.Accounts
{
    public class DeleteRequest
    {
        [Required(ErrorMessage = "User Name is required")]
        public string UserName { get; set; }
    }
}
