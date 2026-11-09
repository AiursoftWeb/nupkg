using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Aiursoft.Nupkg.Services;

public class NuGetJsonOutputFormatter() : SystemTextJsonOutputFormatter(new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
})
{
    public override bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        var action = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return action?.ControllerTypeInfo.Namespace == "Aiursoft.Nupkg.Controllers.Protocol" && base.CanWriteResult(context);
    }
}
