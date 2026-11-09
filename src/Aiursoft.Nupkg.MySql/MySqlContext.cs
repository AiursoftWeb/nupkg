using System.Diagnostics.CodeAnalysis;
using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.MySql;

[ExcludeFromCodeCoverage]

public class MySqlContext(DbContextOptions<MySqlContext> options) : NupkgDbContext(options);
