#nullable disable
// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
namespace Aiursoft.Nupkg.Entities
{
    public class TargetFramework
    {
        public int Key { get; set; }

        public string Moniker { get; set; }

        public Package Package { get; set; }
    }
}
