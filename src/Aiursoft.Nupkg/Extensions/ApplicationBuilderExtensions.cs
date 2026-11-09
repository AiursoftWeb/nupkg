namespace Aiursoft.Nupkg.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseOperationCancelledMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            return app.UseMiddleware<OperationCancelledMiddleware>();
        }
    }
}
