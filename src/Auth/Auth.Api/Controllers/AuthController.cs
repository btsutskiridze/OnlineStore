using Auth.Api.Auth;
using Auth.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Auth.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenConfigurationOptions _jwtConfig;
        private readonly InterServiceAuthenticationOptions _authConfig;

        public AuthController(
            IOptions<JwtTokenConfigurationOptions> jwtConfig,
            IOptions<InterServiceAuthenticationOptions> authConfig)
        {
            _jwtConfig = jwtConfig.Value;
            _authConfig = authConfig.Value;
        }

        [HttpPost("token")]
        public IActionResult IssueDevToken([FromQuery] string role, [FromQuery] string? name, [FromQuery] int? userId)
        {
            var id = string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? 1.ToString()
                : 2.ToString();

            role = string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? UserRoles.Admin : UserRoles.User;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim> {
                new (ClaimTypes.NameIdentifier, id),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? $"{role.ToLower()}" : name!),
                new(ClaimTypes.Role, role)
            };

            var jwt = new JwtSecurityToken(
                issuer: _jwtConfig.Issuer,
                audience: _jwtConfig.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt), role });
        }

        [HttpPost("internal/token")]
        // [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult IssueServiceToken(
            [FromHeader(Name = "X-Client-Id")] string clientId,
            [FromHeader(Name = "X-Client-Secret")] string clientSecret,
            [FromHeader(Name = "X-Audience")] string targetService)
        {
            var client = _authConfig.AuthorizedClients.SingleOrDefault(c => c.ClientId == clientId && c.ClientSecret == clientSecret);

            if (client is null)
                return Unauthorized();

            if (!client.AllowedTargetServices.Contains(targetService))
                return BadRequest("Client not authorized for target service");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim> {
                new("client_id", client.ClientId)
            };

            foreach (var permission in client.GrantedPermissions)
            {
                claims.Add(new("permission", permission));
            }

            var jwt = new JwtSecurityToken(
                issuer: _jwtConfig.Issuer,
                audience: targetService,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt) });
        }
    }
}
