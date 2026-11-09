#nullable disable
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aiursoft.Nupkg.Entities.Converters
{
    public class UriToStringConverter : ValueConverter<Uri, string>
    {
        public static readonly UriToStringConverter Instance = new();

        public UriToStringConverter()
            : base(
                v => v.AbsoluteUri,
                v => new Uri(v))
        {
        }
    }
}
