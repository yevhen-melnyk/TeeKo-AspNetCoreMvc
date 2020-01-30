using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace TeeKoASPCore.Models
{
    public class LoginModel
    {
        [Required]
        [MaxLength(12)]
        [RegularExpression("[A-Za-z0-9]+", ErrorMessage ="Letters and numbers only bruh")]
        public string Name { get; set; }
        [FromQuery]
        public string ReturnUrl { get; set; }
    }
}
