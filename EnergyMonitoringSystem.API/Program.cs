using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using EnergyMonitoringSystem.Service.Auth;
using EnergyMonitoringSystem.Service.Services;
using EnergyMonitoringSystem.Service.Billing;   // Eklendi
using EnergyMonitoringSystem.Service.Emission; // Eklendi
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization; // JSON Ayarları için gerekli

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);
    }));

// 2. Identity Kurulumu
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 3. JWT Authentication
var tokenKey = builder.Configuration["TokenKey"] ?? throw new Exception("TokenKey bulunamadı!");
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// 4. Servislerin Kaydı (Dependency Injection) - TEMİZ HALİ
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<BillingService>();       // Namespace yukarı eklendiği için kısaldı
builder.Services.AddScoped<EmissionService>();      // Namespace yukarı eklendiği için kısaldı
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<ConsumptionFlowService>();

// 5. Arka Plan Servisi (Worker)
builder.Services.AddHostedService<EnergyMonitoringSystem.Service.BackgroundServices.ModbusCollectorWorker>();

// --- KRİTİK GÜNCELLEME BURADA ---
// Parent-Child ilişkisi (Cycle) hatasını önlemek ve Enum'ları string göstermek için:
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Döngüsel referansları yoksay (Meter -> ChildMeter -> ParentMeter döngüsü için şart)
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // Enumları sayı (1,2) yerine yazı ("Daily", "Hourly") olarak göster
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// --------------------------------

// 6. Swagger Ayarları
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Energy Monitoring API", Version = "v1" });

    // Kilit Butonu (Bearer Token)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Örnek: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// 7. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());
});

var app = builder.Build();

// Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("../swagger/v1/swagger.json", "Energy Monitoring API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseMiddleware<EnergyMonitoringSystem.API.Middlewares.ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Veritabanı Seed İşlemleri
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.SeedAdminUser(userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Admin kullanıcısı oluşturulurken hata çıktı.");
    }
}

app.Run();