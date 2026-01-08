using BookShoppingCartMvcUI;
using BookShoppingCartMvcUI.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using BookShoppingCartMvcUI.Services;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
// recommend sản phẩm
builder.Services.AddHttpClient<RecommendationService>();
//email
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.Configure<IdentityOptions>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 2️⃣ Identity
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// 3️⃣ Authentication + Google
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        options.CallbackPath = "/signin-google";

        // ✅ Chỉ ép prompt khi chưa login
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var uri = context.RedirectUri;

            // Xóa prompt cũ (nếu có) và thêm prompt mới
            uri = Regex.Replace(uri, @"(&|\?)prompt=[^&]*", "");
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                uri += "&prompt=select_account";
            }

            context.Response.Redirect(uri);
            return Task.CompletedTask;
        };
    });



// 4️⃣ Razor Runtime Compilation (Fix lỗi FileProvider)
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation(options =>
    {
        options.FileProviders.Add(new PhysicalFileProvider(
            builder.Environment.ContentRootPath));
    });
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

// 5️⃣ Cookie settings (Quan trọng cho Google login)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});
builder.Services.ConfigureExternalCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});
builder.Services.AddSingleton<VnPayService>();

// 6️⃣ Repository injections
builder.Services.AddTransient<IHomeRepository, HomeRepository>();
builder.Services.AddTransient<ICartRepository, CartRepository>();
builder.Services.AddTransient<IUserOrderRepository, UserOrderRepository>();
builder.Services.AddTransient<IStockRepository, StockRepository>();
builder.Services.AddTransient<IGenreRepository, GenreRepository>();
builder.Services.AddTransient<IFileService, FileService>();
builder.Services.AddTransient<IBookRepository, BookRepository>();
builder.Services.AddTransient<IReportRepository, ReportRepository>();
/*builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();*/

var app = builder.Build();

// 7️⃣ Seed dữ liệu admin
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedDefaultData(scope.ServiceProvider);
}

//Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// 🛠️ Thêm để nhận diện HTTPS khi redirect từ Google
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Main}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
