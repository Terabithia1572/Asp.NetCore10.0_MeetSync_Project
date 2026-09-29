using MeetSync.Domain.Entities;
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

        var decodedName = RoomName.Clean(request.RoomName);

        return await _roomService.GetOrCreateRoomAsync(decodedName, request.CreatedBy, cancellationToken);
    }

    public async Task<bool> CanJoinMeetingAsync(string roomName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomName)) return false;
        var decodedName = RoomName.Clean(roomName);
        var room = await _roomService.GetRoomByNameAsync(decodedName, cancellationToken);
        return room != null && room.IsActive;
    }
}
