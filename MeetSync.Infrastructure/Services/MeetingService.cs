using FluentValidation;
using MeetSync.Application.DTOs.Room;
using MeetSync.Application.Interfaces;
using MeetSync.Infrastructure.Persistence;

namespace MeetSync.Infrastructure.Services;

public class MeetingService : IMeetingService
{
    private readonly IRoomService _roomService;
    private readonly IValidator<JoinRoomRequestDto> _joinRoomValidator;

    public MeetingService(
        IRoomService roomService,
        IValidator<JoinRoomRequestDto> joinRoomValidator)
    {
        _roomService = roomService;
        _joinRoomValidator = joinRoomValidator;
    }

    public async Task<RoomResponseDto> PrepareMeetingRoomAsync(JoinRoomRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _joinRoomValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorDict = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new Domain.Exceptions.ValidationException(errorDict);
        }

        var existingRoom = await _roomService.GetRoomByNameAsync(request.RoomName, cancellationToken);
        if (existingRoom != null)
        {
            return existingRoom;
        }

        // If room does not exist yet, auto-create it
        return await _roomService.CreateRoomAsync(new CreateRoomRequestDto(request.RoomName), cancellationToken);
    }

    public async Task<bool> CanJoinMeetingAsync(string roomName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomName)) return false;
        var room = await _roomService.GetRoomByNameAsync(roomName, cancellationToken);
        return room != null && room.IsActive;
    }
}
