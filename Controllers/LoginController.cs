using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TeeKoASPCore.Models;
using TeeKoASPCore.Utility;

namespace TeeKoASPCore.Controllers
{
    public class LoginController : Controller
    {


        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {

            return View(new LoginModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginModel loginModel)
        {

            await Authenticate(loginModel);
            if (!string.IsNullOrEmpty(loginModel.ReturnUrl) && Url.IsLocalUrl(loginModel.ReturnUrl))
            {
                return Redirect(loginModel.ReturnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }

        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Logout(string returnUrl = null)
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }


        [AllowAnonymous]
        public async Task Authenticate(LoginModel model)
        {
            List<Claim> claims = new List<Claim>() {
                new Claim("name", model.Name),
                new Claim ("id", HashUtility.GetShortHash(model.Name+DateTime.Now.ToString()))
            };
            ClaimsIdentity id = new ClaimsIdentity(claims, "CookieAuth");
            ClaimsPrincipal principal = new ClaimsPrincipal(id);

            await HttpContext.SignInAsync("CookieAuth", principal);
        }
    }
}
