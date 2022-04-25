using Chirper.Data;
using Chirper.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;

namespace Chirper.Controllers.Application
{
    public class AccountController : BaseController
    {
        public AccountController(PostgresContext context, IOptions<AppSettings> options) : base(context, options) { }

        #region Account / Register
        [Route("Account/Register")]
        public IActionResult Register()
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Account/Register")]
        public async Task<IActionResult> Register(UserDto dto)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;

            if (!ModelState.IsValid)
                return View(dto);

            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "The hCaptcha failed to verify. Please try again.");
                return View(dto);
            }

            if (dto.Username.ToLower().Equals(dto.Password.ToLower()))
            {
                ModelState.AddModelError(string.Empty, "Your username and password can not be the same.");
                return View(dto);
            }

            if (dto.Username.ToLower().Equals("about") || dto.Username.ToLower().Equals("account") || dto.Username.ToLower().Equals("search") || dto.Username.ToLower().Equals("chirp") || dto.Username.ToLower().Equals("tag") || dto.Username.ToLower().Equals("dashboard"))
            {
                ModelState.AddModelError(string.Empty, "The username entered is a reserved word and can not be used. Please try a different one.");
                return View(dto);
            }

            DateTime timestamp = DateTime.Now;
            DateOnly date = DateOnly.FromDateTime(timestamp);
            string pwdHash = GetHashValue(dto.Password);
            User user = new()
            {
                Username = dto.Username,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                JoinDate = date,
                AccountActive = false,
                PwdHash = pwdHash,
                ActivationKey = NewActivationKey()
            };

            var checks = await postgres.Users.Where(u => u.Username.ToLower().Equals(user.Username.ToLower()) || u.Email.ToLower().Equals(user.Email.ToLower())).ToArrayAsync();
            if (checks.Length != 0)
            {
                var usernameCheck = checks.Where(c => c.Username.ToLower().Equals(user.Username.ToLower())).FirstOrDefault();
                var emailCheck = checks.Where(c => c.Email.ToLower().Equals(user.Email.ToLower())).FirstOrDefault();

                if (usernameCheck != null)
                    ModelState.AddModelError(string.Empty, "That username is already taken. Please try a different one.");

                if (emailCheck != null)
                    ModelState.AddModelError(string.Empty, "That email address is already in use. Please enter a different address.");

                return View(dto);
            }

            postgres.Users.Add(user);
            await postgres.SaveChangesAsync();

            string name = dto.FirstName + " " + dto.LastName;
            string subject = "Activate Your Chirper Account";
            string body = "<h1>The Chirper App</h1>" +
                "<hr/>" +
                "<h3>Activate Your Account</h3>" +
                "<p>Please activate your account by clicking this <a href=\"https://chirper.stephendavis.io/account/activate/" + user.UserId + "/" + user.ActivationKey + "/\">link</a>.</p>";
            bool result = await SendEmail(name, dto.Email, subject, body);

            TempData["message"] = "Please check your email for an account activation link.";
            return RedirectToAction("LogIn", "Account");
        }
        #endregion

        #region Account / Log In
        [Route("Account/LogIn")]
        public IActionResult LogIn(string? returnUrl = null)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            ViewBag.ResetMessage = TempData["ResetPassword"];
            if (!string.IsNullOrWhiteSpace(returnUrl))
                TempData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Account/LogIn")]
        public async Task<IActionResult> LogIn(LogInDto dto)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!ModelState.IsValid)
                return View(dto);

            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "The hCaptcha failed to verify. Please try again.");
                return View(dto);
            }

            string hash = GetHashValue(dto.Password);
            var user = await postgres.Users.Where(u => u.Username.ToLower().Equals(dto.Username.ToLower()) && u.PwdHash.Equals(hash)).FirstOrDefaultAsync();

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No user found with the provided username and password.");
                return View(dto);
            }

            if (!user.AccountActive)
            {
                ModelState.AddModelError(string.Empty, "Please activate your account by clicking on the email sent to you.");
                return View(dto);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString())
            };

            DateTimeOffset timestampOffset = DateTimeOffset.UtcNow;
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                AllowRefresh = true,
                ExpiresUtc = timestampOffset.AddHours(24),
                IssuedUtc = timestampOffset
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            object? returnUrlObj = string.Empty;
            TempData.TryGetValue("ReturnUrl", out returnUrlObj);
            string? returnUrl = returnUrlObj as string;

            if (string.IsNullOrWhiteSpace(returnUrl))
                return RedirectToAction("Index", "Home");

            return Redirect(returnUrl);
        }
        #endregion

        #region Account / Log Out
        [Route("Account/LogOut")]
        public async Task<IActionResult> LogOUt()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
        #endregion

        #region Account / Activate
        [Route("Account/Activate/{user}/{key}")]
        public async Task<IActionResult> Activate(string user, string key)
        {
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(key))
                return RedirectToAction("Register", "Account");

            User? record = await postgres.Users.FindAsync(Guid.Parse(user));

            if (record == null || !record.ActivationKey.Equals(key))
                return RedirectToAction("Register", "Account");

            record.AccountActive = true;
            record.ActivationKey = null;
            postgres.Users.Update(record);
            await postgres.SaveChangesAsync();

            TempData["message"] = "Your account is now active. Please sign in.";
            return RedirectToAction("LogIn", "Account");
        }
        #endregion

        #region Account / Reset Password
        [Route("Account/Reset")]
        public IActionResult ResetPassword()
        {
            ViewBag.PromptUsername = true;
            ViewBag.PromptPassword = false;
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Account/Reset")]
        public async Task<IActionResult> ResetPassword(string username)
        {
            ViewBag.PromptUsername = true;
            ViewBag.PromptPassword = false;
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            
            UserDto dto = new();
            
            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError(string.Empty, "Please provide a valid username.");
                return View(dto);
            }

            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "The hCaptcha failed to verify. Please try again.");
                return View(dto);
            }

            var user = await postgres.Users.Where(u => u.Username.ToLower().Equals(username.ToLower())).FirstOrDefaultAsync();

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No account found with the provided username.");
                return View(dto);
            }

            user.ActivationKey = NewActivationKey();
            user.PwdResetTimestamp = DateTime.UtcNow;

            postgres.Users.Update(user);
            await postgres.SaveChangesAsync();

            string name = user.FirstName + " " + user.LastName;
            string subject = "Reset Your Chirper Password";
            string body = "<h1>The Chirper App</h1>" +
                "<hr/>" +
                "<h3>Activate Your Account</h3>" +
                "<p>Please reset your account password by clicking this <a href=\"https://chirper.stephendavis.io/account/reset/" + user.UserId + "/" + user.ActivationKey + "/\">link</a>.</p>" +
                "<p><strong>Please note this link will expire in 15 minutes.</strong></p>";
            bool result = await SendEmail(name, user.Email, subject, body);

            ModelState.AddModelError(string.Empty, "A password reset email has been sent to the address on file. Please click the link in the email to reset your password. This message may have been sent to your junk or spam folder.");
            return View(dto);
        }

        [Route("Account/Reset/{user}/{key}")]
        public IActionResult ResetPassword(string user, string key)
        {
            ViewBag.PromptUsername = false;
            ViewBag.PromptPassword = true;
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(key))
            {
                ViewBag.PromptUsername = true;
                ViewBag.PromptPassword = false;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Account/Reset/{user}/{key}")]
        public async Task<IActionResult> ResetPassword(UserDto dto, string user, string key)
        {
            ViewBag.PromptUsername = false;
            ViewBag.PromptPassword = true;
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(key))
            {
                ViewBag.PromptUsername = true;
                ViewBag.PromptPassword = false;
                return View();
            }

            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "The hCaptcha failed to verify. Please try again.");
                return View(dto);
            }

            if (!dto.Password.Equals(dto.ConfirmPassword))
            {
                ModelState.AddModelError(string.Empty, "The passwords do not match. Please try again.");
                return View(dto);
            }

            var account = await postgres.Users.FindAsync(Guid.Parse(user));
            
            if (account == null)
            {
                ViewBag.PromptUsername = true;
                ViewBag.PromptPassword = false;
                ModelState.AddModelError(string.Empty, "No account found for the ID provided.");
                return View(dto);
            }

            if (!account.ActivationKey.Equals(key))
            {
                ViewBag.PromptUsername = true;
                ViewBag.PromptPassword = false;
                ModelState.AddModelError(string.Empty, "An invalid activation key was provided.");
                return View(dto);
            }

            long start = ((DateTimeOffset)account.PwdResetTimestamp).ToUnixTimeSeconds();
            long end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool validTime = ((end - start) / 60) <= 15;

            if (!validTime)
            {
                ViewBag.PromptUsername = true;
                ViewBag.PromptPassword = false;
                ModelState.AddModelError(string.Empty, "The reset link has expired. Please submit another password request.");
                return View(dto);
            }

            string pwdHash = GetHashValue(dto.Password);
            account.PwdHash = pwdHash;
            account.ActivationKey = null;
            account.PwdResetTimestamp = null;

            postgres.Users.Update(account);
            await postgres.SaveChangesAsync();

            TempData["ResetPassword"] = "Your password has been updated. Please log in.";
            return RedirectToAction("LogIn", "Account");
        }
        #endregion

        #region Account / Delete
        [Authorize]
        [Route("Account/Delete")]
        public async Task<IActionResult> Delete()
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            string id = GetUserIdClaim();

            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("LogIn", "Account");

            UserDetailsDto? user = await GetUserDetailsDto(id);

            if (user == null)
                return RedirectToAction("LogIn", "Account");

            user.FollowerCount = await GetFollowerCount(user.UserId);
            user.ChirpCount = await GetChirpCount(user.UserId);
            user.LikeCount = await GetLikeCount(user.UserId);

            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Account/Delete")]
        public async Task<IActionResult> Delete(string UserId)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;

            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
                return RedirectToAction("LogOut", "Account");

            string userClaim = GetUserIdClaim();
            Guid id = Guid.Parse(userClaim);
            var user = await postgres.Users.FindAsync(id);
            postgres.Users.Remove(user);
            await postgres.SaveChangesAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
        #endregion
    }
}
