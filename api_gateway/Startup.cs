using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Collections.Generic;

namespace ApiGateway
{
    /// <summary>
    /// Класс Startup для настройки и конфигурации API Gateway
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса Startup
        /// </summary>
        /// <param name="configuration">Конфигурация приложения</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Получает конфигурацию приложения
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Настраивает сервисы приложения
        /// </summary>
        /// <param name="services">Коллекция сервисов</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            
            // Добавляем HttpClient для взаимодействия с другими сервисами
            services.AddHttpClient("FileStoringService", client =>
            {
                client.BaseAddress = new Uri(Configuration["ServiceUrls:FileStoringService"] ?? "http://file-storing-service:8001");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(30); // Устанавливаем таймаут
            });
            
            services.AddHttpClient("FileAnalysisService", client =>
            {
                client.BaseAddress = new Uri(Configuration["ServiceUrls:FileAnalysisService"] ?? "http://file-analysis-service:8002");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(30); // Устанавливаем таймаут
            });
            
            // Добавляем Swagger с расширенной документацией
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { 
                    Title = "Text Analyzer API Gateway", 
                    Version = "v1",
                    Description = "API Gateway для микросервисной архитектуры анализа текстовых файлов",
                    Contact = new OpenApiContact
                    {
                        Name = "Text Analyzer Team",
                        Email = "support@textanalyzer.example.com",
                        Url = new Uri("https://textanalyzer.example.com")
                    }
                });
                
                // Включаем XML-комментарии в Swagger
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
                
                // Добавляем определение безопасности для JWT
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            
            // Добавляем более строгую CORS-политику
            services.AddCors(options =>
            {
                options.AddPolicy("DefaultPolicy", builder =>
                {
                    builder
                        .WithOrigins(
                            Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? 
                            new[] { "http://localhost:3000", "https://textanalyzer.example.com" }
                        )
                        .WithMethods("GET", "POST", "PUT", "DELETE")
                        .WithHeaders("Authorization", "Content-Type")
                        .AllowCredentials();
                });
            });
            
            // Добавляем Rate Limiting для защиты от DDoS
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                });
                
                options.OnRejected = async (context, _) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Please try again later." });
                };
            });
            
            // Добавляем кэширование
            services.AddResponseCaching();
            services.AddMemoryCache();
        }

        /// <summary>
        /// Настраивает конвейер HTTP-запросов
        /// </summary>
        /// <param name="app">Построитель приложения</param>
        /// <param name="env">Среда веб-хостинга</param>
        /// <param name="logger">Логгер</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                logger.LogInformation("Running in Development environment");
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
                logger.LogInformation("Running in Production environment");
            }
            
            // Добавляем глобальную обработку исключений
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    
                    var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                    var exception = exceptionHandlerPathFeature?.Error;
                    
                    logger.LogError(exception, "Unhandled exception occurred");
                    
                    await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again later." });
                });
            });
            
            // Добавляем middleware для логирования запросов
            app.Use(async (context, next) =>
            {
                var requestPath = context.Request.Path;
                var requestMethod = context.Request.Method;
                
                logger.LogInformation("Request received: {Method} {Path}", requestMethod, requestPath);
                
                await next();
                
                logger.LogInformation("Response sent: {Method} {Path} - Status: {StatusCode}", 
                    requestMethod, requestPath, context.Response.StatusCode);
            });
            
            // Добавляем поддержку статических файлов
            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            app.UseSwagger();
            app.UseSwaggerUI(c => 
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Text Analyzer API Gateway v1");
                c.RoutePrefix = "api"; // Swagger UI будет доступен по /api
                c.DocumentTitle = "Text Analyzer API Documentation";
                c.EnableDeepLinking();
                c.DisplayRequestDuration();
            });
            
            app.UseRouting();
            
            // Используем кэширование ответов
            app.UseResponseCaching();
            
            // Используем Rate Limiting
            app.UseRateLimiter();
            
            // Используем CORS с настроенной политикой
            app.UseCors("DefaultPolicy");
            
            // Здесь можно добавить аутентификацию и авторизацию
            // app.UseAuthentication();
            // app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                
                // Эндпоинт для проверки здоровья сервиса
                endpoints.MapGet("/health", async context =>
                {
                    var healthStatus = new Dictionary<string, string>
                    {
                        { "ApiGateway", "ok" }
                    };
                    
                    using var fileServiceClient = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("FileStoringService");
                    using var analysisServiceClient = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("FileAnalysisService");
                    
                    try
                    {
                        var fileResponse = await fileServiceClient.GetAsync("/health");
                        healthStatus["FileService"] = fileResponse.IsSuccessStatusCode ? "ok" : "error";
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error checking FileService health");
                        healthStatus["FileService"] = "error";
                    }
                    
                    try
                    {
                        var analysisResponse = await analysisServiceClient.GetAsync("/health");
                        healthStatus["AnalysisService"] = analysisResponse.IsSuccessStatusCode ? "ok" : "error";
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error checking AnalysisService health");
                        healthStatus["AnalysisService"] = "error";
                    }
                    
                    context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                    await context.Response.WriteAsJsonAsync(healthStatus);
                });
                
                // Эндпоинт для обработки ошибок
                endpoints.MapGet("/error", async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again later." });
                });
            });
        }
    }
}
