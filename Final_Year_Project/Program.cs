using CommonUtilities.Helpers.GoogleAI;
using Final_Year_Project;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AccountsService>();
builder.Services.AddSingleton<SupabaseService>();
builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddSingleton<EnumService>();
builder.Services.AddSingleton<TagSuggestionService>();
builder.Services.AddSingleton<SalesService>();
builder.Services.AddSingleton<ArtistSubscriptionService>();
builder.Services.AddSingleton<IGeminiHelper>(provider =>
{
    var configSection = builder.Configuration.GetSection("Gemini");

    var config = new GeminiConfig
    {
        ApiKey = configSection.GetValue<string>("ApiKey"),
        Model = configSection.GetValue<string>("Model") ?? "models/gemini-1.5-flash-lite",
        EnableGrounding = configSection.GetValue<bool>("EnableGrounding"),
        IsVertex = configSection.GetValue<bool>("IsVertex"),
        ExpressMode = configSection.GetValue<bool>("ExpressMode"),
        GroundingThreshold = configSection.GetValue<double>("GroundingThreshold"),
        GroundingMode = configSection.GetValue<string>("GroundingMode") ?? "DYNAMIC"
    };

    return new GeminiHelper(config);
});
;


// Manager for running one-off expiry passes (shared with hosted service & admin endpoint)
builder.Services.AddSingleton<SubscriptionExpiryManager>();
// Hosted background service: auto-expire subscriptions when end_date is reached
builder.Services.AddHostedService<SubscriptionExpiryService>();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHttpClient();

// Image tagging service (WD14 + CLIP-L)
builder.Services.Configure<ImageTaggingOptions>(builder.Configuration.GetSection("ImageTagging"));
builder.Services.AddScoped<IImageTaggingService>(provider =>
{
    var options = provider.GetRequiredService<IOptions<ImageTaggingOptions>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<ImageTaggingService>>();
    
    // If service is disabled, return mock implementation
    if (!options.Value.Enabled)
    {
        return new MockImageTaggingService(provider.GetRequiredService<ILogger<MockImageTaggingService>>());
    }
    
    return new ImageTaggingService(httpClientFactory, options, logger);
});

// Fraud detection service (CLIP-based AI detection and plagiarism)
builder.Services.Configure<FraudDetectionOptions>(builder.Configuration.GetSection("FraudDetection"));
builder.Services.AddScoped<IFraudDetectionService>(provider =>
{
    var options = provider.GetRequiredService<IOptions<FraudDetectionOptions>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<FraudDetectionService>>();
    
    // If service is disabled, return mock implementation
    if (!options.Value.Enabled)
    {
        return new MockFraudDetectionService(provider.GetRequiredService<ILogger<MockFraudDetectionService>>());
    }
    
    return new FraudDetectionService(httpClientFactory, options, logger);
});

// Add session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    // Policy to restrict access to non-admin users only (regular users)
    options.AddPolicy("NonAdminOnly", policy => 
        policy.RequireAssertion(context =>
            !context.User.IsInRole("admin")));
    
    // Policy for admin-only access
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files to serve additional file types like .clip (Clip Studio Paint)
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".clip"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

app.UseSession();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
