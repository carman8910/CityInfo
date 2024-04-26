using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CityInfo.API.Controllers
{
    [Route("api/authentication")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<AuthenticationController> logger;

        public AuthenticationController(IConfiguration configuration, ILogger<AuthenticationController> logger)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public class AuthenticationRequestBody
        {
            public string? UserName { get; set; }
            public string? Password { get; set; }
        }

        public class CityInfoUser(
            int userId, 
            string userName,
            string firstName,
            string lastName, 
            string city)
        {
            public int UserId { get; } = userId;
            public string UserName { get; } = userName;
            public string FirstName { get; } = firstName;
            public string LastName { get; } = lastName;
            public string City { get; } = city;
        }

        [HttpPost("authenticate")]
        public ActionResult<string> Authenticate(AuthenticationRequestBody authenticationRequestBody)
        {
            // Step 1: validate the username/password
            var user = ValidateUserCredentials(
                authenticationRequestBody.UserName,
                authenticationRequestBody.Password);

            if (user == null)
            {
                return Unauthorized();
            }


            // Step 2: create a token
#pragma warning disable CS8604 // Possible null reference argument.
            var securityKey = new SymmetricSecurityKey(
                Convert.FromBase64String(configuration["Authentication:SecretForKey"]));
#pragma warning restore CS8604 // Possible null reference argument.

            var signingCredentials = new SigningCredentials(
                securityKey, SecurityAlgorithms.HmacSha256 );

            // The claims that
            var claimsToken = new List<Claim>();
            claimsToken.Add(new Claim("sub", user.UserId.ToString()));
            claimsToken.Add(new Claim("given_name", user.FirstName));
            claimsToken.Add(new Claim("family_name", user.LastName));
            claimsToken.Add(new Claim("city", user.City));

            var jwtSecurityToken = new JwtSecurityToken(
                configuration["Authentication:Issuer"],
                configuration["Authentication:Audience"],
                claimsToken,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1),
                signingCredentials);

            var tokenToReturn = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

            return Ok(tokenToReturn);
        }

        private CityInfoUser ValidateUserCredentials(string? userName, string? password)
        {
            // we don't have a user db or table
            // return a new CityInfoUser (values would normally come from your user DB/table)

            //return new CityInfoUser(1, userName ?? string.Empty, "Kevin", "Dockx", "Antwerp");
            return new CityInfoUser(1, userName ?? string.Empty, "Carlos", "Aguilar", "Antwerp");
        }
    }
}
