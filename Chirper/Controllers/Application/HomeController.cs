using Chirper.Data;
using Chirper.Models;
using LinqKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chirper.Controllers.Application
{
    public class HomeController : BaseController
    {
        public HomeController(PostgresContext context) : base(context) { }

        [Route("")]
        public async Task<IActionResult> Index()
        {
            var list = await GetChirpDto(25);
            return View(list);
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> Profile(string id)
        {
            ViewBag.Title = "@" + id;
            ViewBag.ActiveProfile = true;
            Guid userId = await postgres.Users.Where(u => u.Username.ToLower().Equals(id.ToLower())).Select(u => u.UserId).FirstOrDefaultAsync();

            if (userId.Equals(Guid.Empty))
            {
                ViewBag.ActiveProfile = false;
                return View();
            }

            ViewBag.User = await GetUserDetailsDto(userId.ToString());
            var chirps = await GetChirpDto(userId.ToString());
            if (chirps != null)
                ViewBag.Chirps = chirps;
            else
                ViewBag.Chirps = null;

            ViewBag.IsFollowing = false;
            ViewBag.IsSignedIn = false;
            string? userClaim = GetUserIdClaim(true);
            if (!string.IsNullOrWhiteSpace(userClaim))
            {
                ViewBag.IsSignedIn = true;
                var check = await postgres.Followers
                    .Where(f => f.UserId.Equals(userId))
                    .Where(f => f.FollowerId.Equals(Guid.Parse(userClaim)))
                    .FirstOrDefaultAsync();
                if (check != null)
                    ViewBag.IsFollowing = true;
            }

            ViewBag.UserId = userId.ToString();
            return View();
        }

        [Authorize]
        [HttpPost]
        [Route("Follow/{id}")]
        public async Task<IActionResult> ToggleFollow(string id)
        {
            string userClaim = GetUserIdClaim();

            Follower follow = new()
            {
                UserId = Guid.Parse(id),
                FollowerId = Guid.Parse(userClaim)
            };

            var check = await postgres.Followers
                .Where(f => f.UserId.Equals(follow.UserId))
                .Where(f => f.FollowerId.Equals(follow.FollowerId))
                .FirstOrDefaultAsync();

            if (check != null)
            {
                postgres.Followers.Remove(check);
                await postgres.SaveChangesAsync();
                return Json(new
                {
                    success = false,
                    message = "You have stopped following this user."
                });
            }

            postgres.Followers.Add(follow);
            await postgres.SaveChangesAsync();
            return Json(new
            {
                success = true,
                message = "You are now following this user."
            });
        }

        [Route("About")]
        public IActionResult About() => View();

        [HttpGet]
        [Route("Search")]
        public async Task<IActionResult> Search(string q)
        {
            ViewBag.Query = q;
            Console.WriteLine(q);
            ViewBag.IsValid = true;
            if (string.IsNullOrWhiteSpace(q))
            {
                ViewBag.IsValid = false;
                return View();
            }

            var tags = await postgres.TagLists
                .Where(t => t.TagName.ToLower().Contains(q.ToLower()))
                .ToArrayAsync();

            var users = await postgres.Users
                .Where(u => u.Username.ToLower().Contains(q.ToLower()) || u.FirstName.ToLower().Contains(q.ToLower()) || u.LastName.ToLower().Contains(q.ToLower()))
                .Select(u => new UserDetailsDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    JoinDate = u.JoinDate,
                    GravatarCode = GetGravatarCode(u.Email)
                })
                .ToArrayAsync();

            if (users != null)
            {
                foreach(var user in users)
                {
                    user.FollowerCount = await GetFollowerCount(user.UserId);
                    user.ChirpCount = await GetChirpCount(user.UserId);
                    user.LikeCount = await GetLikeCount(user.UserId);
                }
            }

            var chirps = await postgres.Chirps
                .Where(c => c.ChirpBody.ToLower().Contains(q.ToLower()))
                .Join(postgres.Users, c => c.UserId, u => u.UserId, (c, u) => new {c,u})
                .Select(cu => new ChirpDto
                {
                    ChirpId = cu.c.ChirpId,
                    UserId = cu.c.UserId,
                    ChirpTimestamp = cu.c.ChirpTimestamp,
                    ChirpBody = cu.c.ChirpBody,
                    ChirpLikes = cu.c.ChirpLikes,
                    ChirpDislikes = cu.c.ChirpDislikes,
                    Username = cu.u.Username,
                    FirstName = cu.u.FirstName,
                    LastName = cu.u.LastName,
                    GravatarCode = GetGravatarCode(cu.u.Email)
                })
                .ToArrayAsync();

            if (tags.Length == 0 && users.Length == 0 && chirps.Length == 0)
            {
                ViewBag.IsValid = false;
                return View();
            }

            ViewBag.Tags = tags;
            ViewBag.TagCount = tags.Length;
            ViewBag.Users = users;
            ViewBag.UserCount = users.Length;
            ViewBag.Chirps = chirps;
            ViewBag.ChirpCount = chirps.Length;
            return View();
        }

        [Route("{*url}", Order = 999)]
        public IActionResult CatchAll()
        {
            Response.StatusCode = 404;
            return View();
        }
    }
}
