using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.API.Services;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(NetWatchDbContext db, IPasswordHasher<User> hasher, TokenService tokenService) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalized = request.Username.Trim();
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Username == normalized, cancellationToken);
        if (user is null || !user.IsActive || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Credenciales incorrectas o usuario inactivo." });
        return Ok(tokenService.Create(user));
    }
}
