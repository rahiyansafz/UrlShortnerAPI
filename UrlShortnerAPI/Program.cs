using Microsoft.EntityFrameworkCore;

using UrlShortnerAPI.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/short-url", async (UrlDto url, ApiDbContext db, HttpContext ctx) =>
{
    // Validating the input url
    if (!Uri.TryCreate(url.Url, UriKind.Absolute, out var inputUrl))
        return Results.BadRequest("Invalid url has been provided!");

    // Creating a short version of the provided url
    var random = new Random();
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@abcdefghijklmnopqrstuvwxyz";
    var randomStr = new string(Enumerable.Repeat(chars, 8).Select(x => x[random.Next(x.Length)]).ToArray());

    // Mapping the short url with the long one
    var sUrl = new UrlManagement()
    {
        Url = url.Url,
        ShortUrl = randomStr
    };

    // Saving the mapping to the db 
    db.Urls.Add(sUrl);
    await db.SaveChangesAsync();

    // Construct url
    var response = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{sUrl.ShortUrl}";

    return Results.Ok(new UrlShortResponseDto()
    {
        Url = response
    });
})
.WithName("ShortUrl")
.WithOpenApi();

app.MapFallback(async (ApiDbContext db, HttpContext ctx) =>
{
    var path = ctx.Request.Path.ToUriComponent().Trim('/');
    var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortUrl.Trim()
                                                       == path.Trim());

    return urlMatch is null ? Results.BadRequest("Invalid request") : Results.Redirect(urlMatch.Url);
});

app.Run();

class ApiDbContext : DbContext
{
    public virtual DbSet<UrlManagement> Urls { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }
}