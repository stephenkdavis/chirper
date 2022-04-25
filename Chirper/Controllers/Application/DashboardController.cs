using Chirper.Data;
using Chirper.Models;
using LinqKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Chirper.Controllers.Application
{
    [Authorize]
    public class DashboardController : BaseController
    {
        public DashboardController(PostgresContext context, IOptions<AppSettings> options) : base(context, options) { }

        [Route("Dashboard")]
        public async Task<IActionResult> Index()
        {
            string id = GetUserIdClaim();

            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("LogIn", "Account");

            UserDetailsDto? user = await GetUserDetailsDto(id);

            if (user == null)
                return RedirectToAction("LogIn", "Account");

            return View(user);
        }

        [Route("Dashboard/Modify")]
        public async Task<IActionResult> Modify()
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            string userId = GetUserIdClaim();
            var user = await GetUserDetailsDto(userId);
            return View(user);
        }

        [HttpPost]
        [Route("Dashboard/Modify")]
        public async Task<IActionResult> Modify(UserDetailsDto dto)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            string userId = GetUserIdClaim();
            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "The hCaptcha failed to verify. Please try again.");
                return View(dto);
            }

            var user = await postgres.Users.FindAsync(Guid.Parse(userId));
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No user profile could be located. Please sign back in and try again.");
                return View(dto);
            }

            var usernameCheck = await postgres.Users.Where(u => u.Username.ToLower().Equals(dto.Username.ToLower())).FirstOrDefaultAsync();

            if (usernameCheck != null && !user.Username.ToLower().Equals(dto.Username.ToLower()))
            {
                ModelState.AddModelError(string.Empty, "The entered username is already taken. Please try a different one.");
                return View(dto);
            }

            var emailCheck = await postgres.Users.Where(u => u.Email.ToLower().Equals(dto.Email.ToLower())).FirstOrDefaultAsync();

            if (emailCheck != null && !user.Email.ToLower().Equals(dto.Email.ToLower()))
            {
                ModelState.AddModelError(string.Empty, "The entered email address is already in use. Please try a different one.");
                return View(dto);
            }

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Username = dto.Username;

            if (!user.Email.ToLower().Equals(dto.Email.ToLower()))
            {
                user.Email = dto.Email;
                user.ActivationKey = NewActivationKey();
                user.AccountActive = false;

                postgres.Users.Update(user);
                await postgres.SaveChangesAsync();

                string name = dto.FirstName + " " + dto.LastName;
                string subject = "Activate Your Chirper Account";
                string body = "<h1>The Chirper App</h1>" +
                    "<hr/>" +
                    "<h3>Activate Your Account</h3>" +
                    "<p>Please activate your account by clicking this <a href=\"https://chirper.stephendavis.io/account/activate/" + user.UserId + "/" + user.ActivationKey + "/\">link</a>.</p>";
                bool result = await SendEmail(name, dto.Email, subject, body);

                TempData["message"] = "Your email address has been updated. You will be signed out and you must click the activation link that was sent to your new email address. You must do this before being able to sign back into your account.";
                return RedirectToAction("LogOut", "Account");
            }

            postgres.Users.Update(user);
            await postgres.SaveChangesAsync();

            TempData["message"] = "Your changes have been saved.";
            return RedirectToAction("Index", "Dashboard");
        }

        [Route("Dashboard/Followers")]
        public async Task<IActionResult> Followers()
        {
            ViewBag.HasData = false;
            string activeUserId = GetUserIdClaim();
            var list = await postgres.Followers
                .Where(f => f.UserId.Equals(Guid.Parse(activeUserId)))
                .Join(postgres.Users, f => f.FollowerId, u => u.UserId, (f, u) => new { f, u })
                .Select(x => new UserDetailsDto
                {
                    UserId = x.f.FollowerId,
                    Username = x.u.Username,
                    FirstName = x.u.FirstName,
                    LastName = x.u.LastName,
                    JoinDate = x.u.JoinDate,
                    GravatarCode = GetGravatarCode(x.u.Email)
                })
                .OrderBy(x => x.Username)
                .ToArrayAsync();

            if (list.Length > 0)
            {
                foreach (var user in list)
                {
                    user.FollowerCount = await GetFollowerCount(user.UserId);
                    user.ChirpCount = await GetChirpCount(user.UserId);
                    user.LikeCount = await GetLikeCount(user.UserId);
                }
                ViewBag.HasData = true;
            }

            ViewBag.Title = "My Followers";
            ViewBag.Error = "It's lonely without friends. Go find some!";
            return View("Follow", list);
        }

        [Route("Dashboard/Following")]
        public async Task<IActionResult> Following()
        {
            ViewBag.HasData = false;
            string activeUserId = GetUserIdClaim();
            var list = await postgres.Followers
                .Where(f => f.FollowerId.Equals(Guid.Parse(activeUserId)))
                .Join(postgres.Users, f => f.UserId, u => u.UserId, (f, u) => new { f, u })
                .Select(x => new UserDetailsDto
                {
                    UserId = x.f.FollowerId,
                    Username = x.u.Username,
                    FirstName = x.u.FirstName,
                    LastName = x.u.LastName,
                    JoinDate = x.u.JoinDate,
                    GravatarCode = GetGravatarCode(x.u.Email)
                })
                .OrderBy(x => x.Username)
                .ToArrayAsync();

            if (list.Length > 0)
            {
                foreach (var user in list)
                {
                    user.FollowerCount = await GetFollowerCount(user.UserId);
                    user.ChirpCount = await GetChirpCount(user.UserId);
                    user.LikeCount = await GetLikeCount(user.UserId);
                }
                ViewBag.HasData = true;
            }

            ViewBag.Title = "Who I'm Following";
            ViewBag.Error = "You are not following anyone..... yet!";
            return View("Follow", list);
        }
    }
}
