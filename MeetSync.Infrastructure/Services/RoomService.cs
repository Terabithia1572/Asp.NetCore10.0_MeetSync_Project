using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MeetSync.Application.DTOs.Room;
using MeetSync.Application.Interfaces;
using MeetSync.Domain.Entities;
using MeetSync.Domain.Exceptions;
using MeetSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetSync.Infrastructure.Services;

public class RoomService : IRoomService
{
    private readonly MeetSyncDbContext _context;
    private readonly IValidator<CreateRoomRequestDto> _createRoomValidator;

    public RoomService(
        MeetSyncDbContext context,
        IValidator<CreateRoomRequestDto> createRoomValidator)
    {
        _context = context;
        _createRoomValidator = createRoomValidator;
    }

    public async Task<RoomResponseDto> CreateRoomAsync(CreateRoomRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _createRoomValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorDict = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new Domain.Exceptions.ValidationException(errorDict);
        }

        var trimmedName = request.Name.Trim();

        var existingRoom = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == trimmedName && r.IsActive, cancellationToken);

        if (existingRoom != null)
        {
            return MapToDto(existingRoom);
        }

        // HostUserId fallback logic: ensure valid Guid if CreatedBy is null or Guid.Empty
        Guid hostId = (request.CreatedBy.HasValue && request.CreatedBy.Value != Guid.Empty)
            ? request.CreatedBy.Value
            : Guid.NewGuid();

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            CreatedBy = hostId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsLobbyEnabled = request.IsLobbyEnabled,
            IsLocked = false,
            RoomPassword = !string.IsNullOrWhiteSpace(request.Password) ? HashPassword(request.Password) : null
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(room);
    }

    public async Task<RoomResponseDto?> GetRoomByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken);

        if (room == null)
            throw new NotFoundException("Room", id);

        return MapToDto(room);
    }

    public async Task<RoomResponseDto?> GetRoomByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == name.Trim() && r.IsActive, cancellationToken);

        if (room == null) return null;

        return MapToDto(room);
    }

    public async Task<bool> DeactivateRoomAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (room == null) throw new NotFoundException("Room", id);

        room.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetRoomLockAsync(string name, bool isLocked, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Name == name.Trim() && r.IsActive, cancellationToken);
        if (room == null) return false;

        room.IsLocked = isLocked;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> VerifyPasswordAsync(string name, string? password, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == name.Trim() && r.IsActive, cancellationToken);

        if (room == null) return false;
        if (string.IsNullOrEmpty(room.RoomPassword)) return true;

        if (string.IsNullOrEmpty(password)) return false;
        return string.Equals(HashPassword(password), room.RoomPassword, StringComparison.Ordinal);
    }

    private static RoomResponseDto MapToDto(Room room)
    {
        return new RoomResponseDto(
            room.Id,
            room.Name,
            room.CreatedBy,
            room.CreatedAt,
            room.IsActive,
            room.IsLobbyEnabled,
            room.IsLocked,
            !string.IsNullOrEmpty(room.RoomPassword)
        );
    }

    private static string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
