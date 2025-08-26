using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;
using ElectionAdminPanel.Web.BackgroundServices;
using ElectionAdminPanel.Web.Controllers;
using ElectionAdminPanel.Web.Services;
using ElectionAdminPanel.Web.Filters;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel server limits
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestHeadersTotalSize = 131072; // 128KB (increased from 64KB)
    serverOptions.Limits.MaxRequestHeaderCount = 200; // Increased from 100
    serverOptions.Limits.MaxRequestBodySize = 52428800; // 50MB
    serverOptions.Limits.MaxRequestBufferSize = 1048576; // 1MB
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure form options
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartHeadersLengthLimit = 131072; // 128KB
    options.MultipartBodyLengthLimit = 52428800; // 50MB
    options.ValueLengthLimit = 4194304; // 4MB
    options.KeyLengthLimit = 4096; // 4KB
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "ElectionAuth"; // Shorter cookie name
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

// Add Data Protection services
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "DataProtection-Keys")));

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.MaxAge = TimeSpan.FromMinutes(30);
    options.Cookie.Name = "ElectionSession"; // Shorter cookie name
});

// Add HttpClient for API calls
builder.Services.AddHttpClient();

// Add HttpContextAccessor for accessing HttpContext in services
builder.Services.AddHttpContextAccessor();

// Add Newtonsoft.Json for JSON serialization/deserialization
builder.Services.AddMvc().AddNewtonsoftJson();

// Register the sealed election service
builder.Services.AddScoped<ISealedElectionService, SealedElectionService>();

// Register the sealed election restriction filter
builder.Services.AddScoped<SealedElectionRestrictionFilter>();

// Register the background service
builder.Services.AddHostedService<ZeresimaReportService>();

// Register the ReportController
builder.Services.AddTransient<ReportController>();

// Register the EmailTemplateService
builder.Services.AddScoped<ElectionAdminPanel.Web.Services.IEmailTemplateService, ElectionAdminPanel.Web.Services.EmailTemplateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Use session middleware
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();