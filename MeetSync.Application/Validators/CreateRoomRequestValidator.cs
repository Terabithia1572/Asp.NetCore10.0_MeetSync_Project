using FluentValidation;
using MeetSync.Application.DTOs.Room;

namespace MeetSync.Application.Validators;

public class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequestDto>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .Length(2, 100).WithMessage("Room name must be between 2 and 100 characters.")
            .Matches(@"^[a-zA-Z0-9\s\-_çğıöşüÇĞİÖŞÜ]+$").WithMessage("Room name can only contain letters (including Turkish characters), numbers, spaces, hyphens, and underscores.");
    }
}
