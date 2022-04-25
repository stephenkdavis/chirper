using Chirper.Data;
using Chirper.Models;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chirper.Controllers
{
    public class BaseController : Controller
    {
        protected readonly PostgresContext postgres;
        protected readonly IOptions<AppSettings> settings;
        private static HttpClient client = new HttpClient();

        public BaseController() { }
        public BaseController(PostgresContext context) => postgres = context;
        public BaseController(PostgresContext context, IOptions<AppSettings> services)
        {
            postgres = context;
            settings = services;
        }

        protected static string GetGravatarCode(string email)
        {
            MD5 hasher = MD5.Create();
            byte[] data = hasher.ComputeHash(Encoding.Default.GetBytes(email.ToLower()));
            StringBuilder builder = new();
            foreach (var letter in data)
                builder.Append(letter.ToString("x2"));
            return builder.ToString();
        }

        protected static string GetHashValue(string text)
        {
            byte[]? temp = null;

            using (HashAlgorithm algorithm = SHA256.Create())
                temp = algorithm.ComputeHash(Encoding.UTF8.GetBytes(text));

            StringBuilder sb = new();
            foreach (byte b in temp)
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        protected async Task<bool> VerifyHcaptcha(string token)
        {
            string baseUrl = "https://hcaptcha.com/siteverify";
            List<KeyValuePair<string, string>> postData = new()
            {
                new KeyValuePair<string, string>("secret", settings.Value.HCaptcha.SecretKey),
                new KeyValuePair<string, string>("response", token)
            };

            HttpResponseMessage response = await client.PostAsync(baseUrl, new FormUrlEncodedContent(postData));

            if (!response.IsSuccessStatusCode)
                return false;

            string body = await response.Content.ReadAsStringAsync();
            JsonDocument json = JsonDocument.Parse(body);
            JsonElement element = json.RootElement;
            bool status = bool.Parse(element.GetProperty("success").ToString());
            Console.WriteLine(status);
            return status;
        }
        protected static string NewActivationKey()
        {
            Guid key1 = Guid.NewGuid();
            Guid key2 = Guid.NewGuid();
            return key1.ToString() + "-" + key2.ToString();
        }

        protected async Task<bool> SendEmail(string name, string email, string subject, string body)
        {
            string source = settings.Value.EmailAccount.Address;
            string display = settings.Value.EmailAccount.DisplayName;
            string password = settings.Value.EmailAccount.Password;

            MailAddress sender = new(source, display);
            MailAddress recipient = new(email, name);

            MailMessage message = new();
            message.IsBodyHtml = true;
            message.From = sender;
            message.To.Add(recipient);
            message.Subject = subject;
            message.Body = body;

            SmtpClient client = new()
            {
                Host = settings.Value.EmailAccount.Host,
                Port = settings.Value.EmailAccount.Port,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(source, password)
            };

            client.Send(message);

            return true;
        }

        protected string GetUserIdClaim()
        {
            return User.Claims.First(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
        }

        protected string? GetUserIdClaim(bool nullable)
        {
            return User.Claims.FirstOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier))?.Value;
        }

        protected async Task<long> GetChirpCount(Guid userId)
        {
            return await postgres.Chirps.Where(c => c.UserId.Equals(userId)).CountAsync();
        }

        protected async Task<long> GetFollowerCount(Guid userId)
        {
            return await postgres.Followers.Where(f => f.UserId.Equals(userId)).CountAsync();
        }

        protected async Task<long> GetLikeCount(Guid userId)
        {
            long result = 0;
            var chirps = await postgres.Chirps.Where(c => c.UserId.Equals(userId)).ToArrayAsync();
            foreach (var chirp in chirps)
                result += chirp.ChirpLikes;
            return result;
        }

        protected async Task<UserDetailsDto[]?> GetUserDetailsDto()
        {
            var list = await postgres.Users
                .Select(u => new UserDetailsDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    JoinDate = u.JoinDate,
                    AccountActive = u.AccountActive,
                    GravatarCode = GetGravatarCode(u.Email)
                })
                .ToArrayAsync();

            if (list.Length == 0)
                return null;

            foreach (var user in list)
            {
                user.FollowerCount = await GetFollowerCount(user.UserId);
                user.ChirpCount = await GetChirpCount(user.UserId);
                user.LikeCount = await GetLikeCount(user.UserId);
            }

            return list;
        }

        protected async Task<UserDetailsDto?> GetUserDetailsDto(string userId)
        {
            UserDetailsDto? user = await postgres.Users
                .Where(u => u.UserId.Equals(Guid.Parse(userId)))
                .Select(u => new UserDetailsDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    JoinDate = u.JoinDate,
                    AccountActive = u.AccountActive,
                    GravatarCode = GetGravatarCode(u.Email)
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            user.FollowerCount = await GetFollowerCount(user.UserId);
            user.ChirpCount = await GetChirpCount(user.UserId);
            user.LikeCount = await GetLikeCount(user.UserId);

            return user;
        }

        protected async Task<ChirpDto[]?> GetChirpDto(string userId = "", string chirpId = "")
        {
            var chirps = await postgres.Chirps
                .Where(GetPredicates(userId, chirpId))
                .Join(postgres.Users, c => c.UserId, u => u.UserId, (c, u) => new { c, u })
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
                .OrderByDescending(x => x.ChirpTimestamp)
                .ToArrayAsync();

            if (chirps.Length == 0)
                return null;

            foreach (var chirp in chirps)
                chirp.Tags = await GetTagList(chirp.ChirpId);

            return chirps;
        }

        protected async Task<ChirpDto[]?> GetChirpDto(int records, string userId = "", string chirpId = "")
        {
            var chirps = await postgres.Chirps
                .Where(GetPredicates(userId, chirpId))
                .Join(postgres.Users, c => c.UserId, u => u.UserId, (c, u) => new { c, u })
                .Take(records)
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
                .OrderByDescending(x => x.ChirpTimestamp)
                .ToArrayAsync();

            if (chirps.Length == 0)
                return null;

            foreach (var chirp in chirps)
                chirp.Tags = await GetTagList(chirp.ChirpId);

            return chirps;
        }

        private ExpressionStarter<Chirp> GetPredicates(string userId = "", string chirpId = "")
        {
            var predicate = PredicateBuilder.New<Chirp>(true);

            if (!string.IsNullOrWhiteSpace(userId))
                predicate = predicate.And(x => x.UserId.Equals(Guid.Parse(userId)));

            if (!string.IsNullOrWhiteSpace(chirpId))
                predicate = predicate.And(x => x.ChirpId.Equals(Guid.Parse(chirpId)));

            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(chirpId))
                predicate = predicate.And(x => x.ChirpBody != null);

            return predicate;
        }

        private async Task<TagList[]> GetTagList(Guid chirpId)
        {
            return await postgres.ChirpTags
                .Where(t => t.ChripId.Equals(chirpId))
                .Join(postgres.TagLists, t => t.TagId, l => l.TagId, (t, l) => new { t, l })
                .Select(tl => new TagList
                {
                    TagId = tl.t.TagId,
                    TagName = tl.l.TagName
                })
                .OrderBy(t => t.TagName)
                .ToArrayAsync();
        }
    }
}
