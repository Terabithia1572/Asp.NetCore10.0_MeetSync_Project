using System.Globalization;
using System.Text;

namespace MeetSync.Domain.Entities;

public static class RoomName
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    // MVC already decodes form/query values. Never URL-decode these values again.
    public static string Clean(string? name) => (name ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
    public static string Key(string? name) => Clean(name).ToLower(Turkish);
}
