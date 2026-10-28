using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using CustomerPortalAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ContentStatuses.json", optional: false, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800; // 50 MB total upload
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
});

// Register services
builder.Services.AddSingleton<ContentStatusCatalog>();
builder.Services.AddHttpClient<ZohoServices>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<Auth0Services>();
builder.Services.AddScoped<QuickBookServices>();
builder.Services.AddScoped<GoogleServices>();
builder.Services.AddScoped<DeliverableFileApprovalService>();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ Order matters:
app.UseHttpsRedirection();  // 1️⃣ Redirect first
app.UseCors("AllowAll");    // 2️⃣ Enable CORS before anything else that handles requests
app.UseAuthorization();     // 3️⃣ Then auth
app.MapControllers();       // 4️⃣ Finally map endpoints

app.Run();
