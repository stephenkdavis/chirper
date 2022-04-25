using Chirper.Data;
using Chirper.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chirper.Controllers.Application
{
    public class TagController : BaseController
    {
        public TagController(PostgresContext context, IOptions<AppSettings> options) : base(context, options) { }

        [Route("Tag/List")]
        public async Task<IActionResult> List()
        {
            var tags = await postgres.TagLists.OrderBy(t => t.TagName).ToArrayAsync();
            ViewBag.TagCount = tags.Length;
            return View(tags);
        }

        [HttpGet]
        [Route("Tag/{id}")]
        public async Task<IActionResult> Tag(string id)
        {
            var name = await postgres.TagLists.Where(t => t.TagId.Equals(Guid.Parse(id))).Select(t => t.TagName).FirstOrDefaultAsync();
            var chirps = await postgres.Chirps
                .Join(postgres.Users, c => c.UserId, u => u.UserId, (c, u) => new { c, u })
                .Join(postgres.ChirpTags, cu => cu.c.ChirpId, t => t.ChripId, (cu, t) => new { cu, t })
                .Where(cut => cut.t.TagId.Equals(Guid.Parse(id)))
                .Select(cut => new ChirpDto
                {
                    ChirpId = cut.cu.c.ChirpId,
                    UserId = cut.cu.u.UserId,
                    ChirpTimestamp = cut.cu.c.ChirpTimestamp,
                    ChirpBody = cut.cu.c.ChirpBody,
                    ChirpLikes = cut.cu.c.ChirpLikes,
                    ChirpDislikes = cut.cu.c.ChirpDislikes,
                    Username = cut.cu.u.Username,
                    FirstName = cut.cu.u.FirstName,
                    LastName = cut.cu.u.LastName,
                    GravatarCode = GetGravatarCode(cut.cu.u.Email)
                })
                .OrderByDescending(c => c.ChirpTimestamp)
                .ToArrayAsync();

            if (chirps.Length > 0)
            {
                foreach (var chirp in chirps)
                    chirp.Tags = await postgres.ChirpTags
                        .Where(t => t.ChripId.Equals(chirp.ChirpId))
                        .Join(postgres.TagLists, t => t.TagId, l => l.TagId, (t, l) => new { t, l })
                        .Select(tl => new TagList
                        {
                            TagId = tl.t.TagId,
                            TagName = tl.l.TagName
                        })
                        .OrderBy(t => t.TagName)
                        .ToArrayAsync();
            }

            ViewBag.Name = name;
            ViewBag.ChirpCount = chirps.Length;
            return View(chirps);
        }

        [Authorize]
        [Route("Tag/Create")]
        public IActionResult Create()
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Tag/Create")]
        public async Task<IActionResult> Create(TagList dto)
        {
            ViewBag.SiteKey = settings.Value.HCaptcha.SiteKey;

            string hcaptchaResponse = Request.Form["h-captcha-response"];
            bool hcaptchaValid = await VerifyHcaptcha(hcaptchaResponse);

            if (!hcaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "The hCaptcha failed to verify. Please try again.");
                return View(dto);
            }

            var check = await postgres.TagLists.Where(t => t.TagName.ToLower().Equals(dto.TagName.ToLower())).ToArrayAsync();

            if (check.Length != 0)
            {
                ModelState.AddModelError(string.Empty, "This tag already exists. Please try a different name.");
                return View(dto);
            }

            postgres.TagLists.Add(dto);
            await postgres.SaveChangesAsync();

            return RedirectToAction("Tag", "Tag", new { id = dto.TagId.ToString() });
        }
    }
}
