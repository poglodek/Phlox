using Microsoft.EntityFrameworkCore;
using Phlox.API.Configuration;
using Phlox.API.Data;
using Phlox.API.Extensions;
using Phlox.API.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddPhloxCors(builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuration
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection(QdrantOptions.SectionName));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.Configure<DocumentSlicerOptions>(builder.Configuration.GetSection(DocumentSlicerOptions.SectionName));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));

// Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IDocumentSlicerService, DocumentSlicerService>();
builder.Services.AddSingleton<IEmbeddingService, OpenAiEmbeddingService>();
builder.Services.AddSingleton<IHtmlContentCleanerService, HtmlContentCleanerService>();
builder.Services.AddScoped<IVectorService, VectorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.Services.GetService<ILoggerFactory>()!.CreateLogger<Program>().LogInformation("Go to https://localhost:7086/scalar to use web documentation!");
}

app.UseHttpsRedirection();
app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
