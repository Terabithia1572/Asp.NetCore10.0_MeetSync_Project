using FluentValidation;
using MeetSync.Application.DTOs.Room;

namespace MeetSync.Application.Validators;

public class JoinRoomRequestValidator : AbstractValidator<JoinRoomRequestDto>
{
    public JoinRoomRequestValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Room name is required.")
            .MinimumLength(2).WithMessage("Room name must be at least 2 characters.")
            .Matches(@"^[a-zA-Z0-9\s\-_çğıöşüÇĞİÖŞÜ]+$").WithMessage("Room name can only contain letters (including Turkish characters), numbers, spaces, hyphens, and underscores.");
    }
}
