extern alias AzureIdentityAlias;
using AzureIdentity = AzureIdentityAlias::Azure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Intranet.Models;
using OfficeOpenXml;
using Intranet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Google.Apis.Auth.OAuth2;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new AzureIdentity.DefaultAzureCredential());
}

ExcelPackage.License.SetNonCommercialPersonal("Vumela");

// Increase request size limits for file uploads (must be set before build)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
    options.ValueCountLimit = 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100MB
});

// 1. ADD SERVICES (Before builder.Build)
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Index");
});

// Database Connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration["DefaultConnection"]));

//custom services
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IAzureBlobService, AzureBlobService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(policyName: "AiValidationPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

// Authentication Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    });

builder.Services.AddScoped<ProcurementReportService>();
builder.Services.AddScoped<RegisterService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddHostedService<ReportEmailWorker>();
builder.Services.AddHostedService<MonthlyPaymentWorker>();
builder.Services.AddHttpClient<GeminiAgentService>();
builder.Services.AddHostedService<PaymentDeadlineWorker>();

builder.Services.AddSingleton(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var serviceAccountKeyPath = configuration["Gemini:ServiceAccountKeyPath"];

    GoogleCredential credential;
    if (!string.IsNullOrEmpty(serviceAccountKeyPath) && File.Exists(serviceAccountKeyPath))
    {
        credential = GoogleCredential.FromFile(serviceAccountKeyPath);
    }
    else
    {
        var jsonContent = configuration["GeminiApiKey"];
        if (string.IsNullOrEmpty(jsonContent))
        {
            throw new InvalidOperationException("Gemini credentials could not be found in local files or Key Vault.");
        }
        credential = GoogleCredential.FromJson(jsonContent);
    }

    return credential;
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// 2. CONFIGURE PIPELINE (Middleware)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapRazorPages();
app.Run();