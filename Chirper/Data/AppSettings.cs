namespace Chirper.Data
{
    public class EmailAccount
    {
        public string Address { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Host { get; set; } = null!;
        public int Port { get; set; }
    }

    public class Hcaptcha
    {
        public string SiteKey { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
    }

    public class AppSettings
    {
        public EmailAccount EmailAccount { get; set; } = null!;
        public Hcaptcha HCaptcha { get; set; } = null!;
        public string PostgresRDS { get; set; } = null!;
    }
}
