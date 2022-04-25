using Chirper.Data;
using Chirper.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Chirper.Controllers.Application
{
    public class ChirpController : BaseController
    {
        public ChirpController(PostgresContext context, IOptions<AppSettings> options) : base(context, options) { }

        #region Chirp / View
        [HttpGet]
        [Route("Chirp/{id}")]
        public async Task<IActionResult> View(string id)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            if (!Guid.TryParse(id, out Guid chirpId))
            {
                ViewBag.ValidChirp = false;
                ViewBag.Message = "The provided chirp ID is invalid.";
                return View();
            }

            ViewBag.ValidChirp = true;
            var list = await GetChirpDto(1, "", id);

            if (list == null)
            {
                ViewBag.ValidChirp = false;
                ViewBag.Message = "No chirp found with the provided ID.";
                return View();
            }

            var chirp = list[0];

            var comments = await postgres.Comments
                .Where(c => c.ChirpId.Equals(chirpId))
                .Join(postgres.Users, c => c.UserId, u => u.UserId, (c, u) => new { c, u })
                .Select(cu => new CommentDto
                {
                    UserId = cu.c.UserId,
                    CommentTimestamp = cu.c.CommentTimestamp,
                    CommentBody = cu.c.CommentBody,
                    Username = cu.u.Username,
                    FirstName = cu.u.FirstName,
                    LastName = cu.u.LastName,
                    GravatarCode = GetGravatarCode(cu.u.Email)
                })
                .OrderByDescending(a => a.CommentTimestamp)
                .ToArrayAsync();

            ViewBag.Chirp = chirp;
            ViewBag.Tags = chirp.Tags;
            ViewBag.TagCount = chirp.Tags.Length;
            ViewBag.Comments = comments;
            ViewBag.CommentCount = comments.Length;
            ViewBag.ChirpId = id;
            return View();
        }

        [Authorize]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Chirp/{id}/Rate")]
        public async Task<IActionResult> PostRating(string chirpId, int rate)
        {
            Guid guid = Guid.Parse(chirpId);
            if (guid.Equals(Guid.Empty))
                return Json(new
                {
                    success = false,
                    message = "Invalid Chirp ID."
                });

            var chirp = await postgres.Chirps.FindAsync(guid);
            if (chirp == null)
                return Json(new
                {
                    success = false,
                    message = "No Chirp associated with the provided ID."
                });

            if (rate > 0)
                chirp.ChirpLikes++;

            if (rate < 0)
                chirp.ChirpDislikes++;

            if (rate == 0)
                return Json(new
                {
                    success = false,
                    message = "Invalid Rating."
                });

            postgres.Chirps.Update(chirp);
            await postgres.SaveChangesAsync();
            return Json(new
            {
                success = true,
                message = "Your rating has been recorded."
            });
        }

        [Authorize]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Chirp/{id}/Comment")]
        public async Task<IActionResult> PostComment(string chirpId, string comment)
        {
            string id = GetUserIdClaim();

            Comment newComment = new()
            {
                ChirpId = Guid.Parse(chirpId),
                UserId = Guid.Parse(id),
                CommentBody = comment
            };

            postgres.Comments.Add(newComment);
            int result = await postgres.SaveChangesAsync();

            if (result == 0)
                return BadRequest();

            return Ok();
        }
        #endregion

        #region Chirp / Create
        [Authorize]
        [Route("Chirp/Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            ViewBag.Tags = await postgres.TagLists.OrderBy(t => t.TagName).ToArrayAsync();
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Chirp/Create")]
        public async Task<IActionResult> Create(ChirpDto dto)
        {
            string activeUserId = User.Claims.First(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;

            Chirp chirp = new()
            {
                UserId = Guid.Parse(activeUserId),
                ChirpBody = dto.ChirpBody,
                ChirpLikes = 0,
                ChirpDislikes = 0
            };

            postgres.Chirps.Add(chirp);
            await postgres.SaveChangesAsync();

            if (dto.SelectedTags != null)
            {
                foreach (var tag in dto.SelectedTags)
                {
                    ChirpTag temp = new()
                    {
                        ChripId = chirp.ChirpId,
                        TagId = Guid.Parse(tag)
                    };
                    postgres.ChirpTags.Add(temp);
                }
                await postgres.SaveChangesAsync();
            }

            return RedirectToAction("View", "Chirp", new { id = chirp.ChirpId.ToString() });
        }
        #endregion
    }
}
