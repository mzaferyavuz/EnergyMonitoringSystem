using EnergyMonitoringSystem.Data;
using EnergyMonitoringSystem.Service.Auth;
using EnergyMonitoringSystem.Core.Entities; // ApplicationUser için gerekli
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Swagger ayarları için
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions=>
    {
        // Bağlantı koparsa veya yavaşsa 5 kere daha dene, 30 saniye bekle
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);

        // Komut zaman aşımını uzat (60 saniye)
        sqlOptions.CommandTimeout(60);
    }));

// 2. Identity Kurulumu (Kullanıcı Yönetimi)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 3. JWT Authentication Ayarları (Token Doğrulama)
var tokenKey = builder.Configuration["TokenKey"]
    ?? throw new Exception("TokenKey appsettings.json dosyasında bulunamadı!");

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
        ValidateIssuer = false, // Geliştirme aşamasında kapalı
        ValidateAudience = false
    };
});

// 4. Servislerin Kaydı (Dependency Injection)
builder.Services.AddScoped<ITokenService, TokenService>();
// Eğer BillingService ve EmissionService dosyalarını oluşturduysan onları da buraya eklemelisin:
// builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<EnergyMonitoringSystem.Service.Emission.EmissionService>();
builder.Services.AddScoped<EnergyMonitoringSystem.Service.Billing.BillingService>();

// 5. Arka Plan Servisi (Worker)
// ModbusCollectorWorker servisini kaydet
builder.Services.AddHostedService<EnergyMonitoringSystem.Service.BackgroundServices.ModbusCollectorWorker>();

builder.Services.AddControllers();

// 6. Swagger Ayarları (Kilit Butonu Eklemek İçin)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Tam yol belirterek karışıklığı önlüyoruz: Microsoft.OpenApi.Models.OpenApiInfo
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Energy Monitoring API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Örnek: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
    //var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    //var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    //c.IncludeXmlComments(xmlPath);
});

// 7. CORS (Frontend Erişimi İçin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());
});

var app = builder.Build();

// HTTP Request Pipeline
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//bu kisim gecici test icin eklendi
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Energy Monitoring API v1");
    c.RoutePrefix = "swagger"; // Adresin http://localhost:xxxx/swagger olmasını sağlar
});

app.UseCors("AllowAll"); // CORS'u aktif et

app.UseMiddleware<EnergyMonitoringSystem.API.Middlewares.ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication(); // Önce kimlik doğrulama
app.UseAuthorization();  // Sonra yetkilendirme

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        // Seed işlemini başlat (await kullanmak için Program.cs task yapısına uygun olmalı veya .Wait() kullanılmalı)
        await DbInitializer.SeedAdminUser(userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Admin kullanıcısı oluşturulurken hata çıktı.");
    }
}

app.Run();