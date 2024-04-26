namespace CityInfo.API.Services
{
    public interface IMailService
    {
        void SendEmail(string subject, string message);
    }
}