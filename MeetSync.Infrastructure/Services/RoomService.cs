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

    // Exact equality treats underscores literally; use the same Turkish casing as SignalR.
    // For legacy duplicates, always resolve the oldest active room.
    private IOrderedQueryable<Room> ActiveRoomsNamed(string name) =>
        _context.Rooms.Where(r => r.IsActive &&
            EF.Functions.Collate(r.Name.Trim(), "Turkish_100_CI_AS") == name)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);

    public RoomService(
        MeetSyncDbContext context,
        IValidator<CreateRoomRequestDto> createRoomValidator)
    {
        _context = context;
        _createRoomValidator = createRoomValidator;
    }

    private static string NormalizeRoomName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return RoomName.Clean(name);
    }

    public async Task<RoomResponseDto> CreateRoomAsync(CreateRoomRequestDto request, CancellationToken cancellationToken = default)
    {
        request = request with { Name = NormalizeRoomName(request.Name) };
        var validationResult = await _createRoomValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorDict = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new Domain.Exceptions.ValidationException(errorDict);
        }

        var normName = request.Name;
        // SQL transaction-owned lock serializes creation across app instances, without a migration.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'MeetSync:CreateActiveRoom',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51000, 'Room creation lock could not be acquired.', 1;
            """, cancellationToken);
        var existingRoom = await ActiveRoomsNamed(normName).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (existingRoom != null)
        {
            await transaction.CommitAsync(cancellationToken);
            return MapToDto(existingRoom);
        }

        // HostUserId fallback logic: ensure valid Guid if CreatedBy is null or Guid.Empty
        Guid hostId = (request.CreatedBy.HasValue && request.CreatedBy.Value != Guid.Empty)
            ? request.CreatedBy.Value
            : Guid.NewGuid();

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = normName,
            CreatedBy = hostId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsLobbyEnabled = request.IsLobbyEnabled,
            IsLocked = false,
            RoomPassword = !string.IsNullOrWhiteSpace(request.Password) ? HashPassword(request.Password) : null
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return MapToDto(room);
    }

    public async Task<RoomResponseDto> GetOrCreateRoomAsync(string name, Guid? createdBy = null, CancellationToken cancellationToken = default)
    {
        var normName = NormalizeRoomName(name);
        var existingRoom = await GetRoomByNameAsync(normName, cancellationToken);
        if (existingRoom != null)
        {
            return existingRoom;
        }

        return await CreateRoomAsync(new CreateRoomRequestDto(normName, CreatedBy: createdBy), cancellationToken);
    }

    public async Task<RoomResponseDto> GetOrCreateRoomAsync(CreateRoomRequestDto request, CancellationToken cancellationToken = default)
    {
        return await CreateRoomAsync(request, cancellationToken);
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

        var decodedName = NormalizeRoomName(name);
        var room = await ActiveRoomsNamed(decodedName).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

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
        if (string.IsNullOrWhiteSpace(name)) return false;

        var decodedName = NormalizeRoomName(name);
        var room = await ActiveRoomsNamed(decodedName).FirstOrDefaultAsync(cancellationToken);

        if (room == null) return false;

        room.IsLocked = isLocked;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> VerifyPasswordAsync(string name, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var decodedName = NormalizeRoomName(name);
        var room = await ActiveRoomsNamed(decodedName).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

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
