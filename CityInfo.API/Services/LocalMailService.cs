namespace CityInfo.API.Services
{
    public class LocalMailService : IMailService
    {
        private readonly IConfiguration configuration;
        private string mailTo = string.Empty;
        private string mailFrom = string.Empty;

        public LocalMailService(IConfiguration configuration)
        {
            mailTo = configuration["mailSettings:mailToAddress"];
            mailFrom = configuration["mailSettings:mailFromAddress"];
        }

        public void SendEmail(string subject, string message)
        {
            Console.WriteLine($"Mail from {mailFrom} to {mailTo}, with {nameof(LocalMailService)}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Message: {message}");
        }
    }
}
