using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MeetSync.Application.DTOs.Auth;
using MeetSync.Application.Interfaces;
using MeetSync.Domain.Entities;
using MeetSync.Domain.Exceptions;
using MeetSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetSync.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly MeetSyncDbContext _context;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RegisterRequestDto> _registerValidator;

    public AuthService(
        MeetSyncDbContext context,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RegisterRequestDto> registerValidator)
    {
        _context = context;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorDict = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new Domain.Exceptions.ValidationException(errorDict);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            return new AuthResponseDto(false, "Invalid email address or password.");
        }

        var userDto = new UserResponseDto(user.Id, user.Email, user.DisplayName, user.CreatedAt);
        return new AuthResponseDto(true, "Login successful.", userDto);
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorDict = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new Domain.Exceptions.ValidationException(errorDict);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (existingUser)
        {
            throw new BusinessRuleException("An account with this email address already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserResponseDto(user.Id, user.Email, user.DisplayName, user.CreatedAt);
        return new AuthResponseDto(true, "Registration successful.", userDto);
    }

    public async Task<UserResponseDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            throw new NotFoundException("User", id);

        return new UserResponseDto(user.Id, user.Email, user.DisplayName, user.CreatedAt);
    }

    private static string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        string computedHash = HashPassword(password);
        return string.Equals(computedHash, storedHash, StringComparison.Ordinal);
    }
}
