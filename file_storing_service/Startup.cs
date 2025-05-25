using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Reflection;
using FileStoringService.Services;
using FileStoringService.Services.Validation;

namespace FileStoringService
{
    /// <summary>
    /// Класс Startup для настройки и конфигурации сервиса хранения файлов
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
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });
            
            // Добавляем сервисы для работы с файлами
            services.AddSingleton<IFileService, FileService>();
            
            // Добавляем сервис валидации
            services.AddScoped<IFileValidationService, FileValidationService>();
            
            // Добавляем Swagger с расширенной документацией
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { 
                    Title = "File Storing Service", 
                    Version = "v1",
                    Description = "Микросервис для хранения и управления текстовыми файлами",
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
            
            // Добавляем валидацию модели
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value.Errors.Count > 0)
                        .Select(e => new
                        {
                            Field = e.Key,
                            Errors = e.Value.Errors.Select(er => er.ErrorMessage).ToArray()
                        })
                        .ToArray();

                    return new BadRequestObjectResult(new { errors });
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
            
            app.UseSwagger();
            app.UseSwaggerUI(c => 
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "File Storing Service v1");
                c.RoutePrefix = string.Empty; // Swagger UI будет доступен по корневому URL
                c.DocumentTitle = "File Storing Service API Documentation";
                c.EnableDeepLinking();
                c.DisplayRequestDuration();
            });
            
            app.UseRouting();
            
            // Используем кэширование ответов
            app.UseResponseCaching();
            
            // Используем CORS с настроенной политикой
            app.UseCors("DefaultPolicy");
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                
                // Эндпоинт для проверки здоровья сервиса
                endpoints.MapGet("/health", async context =>
                {
                    context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                    await context.Response.WriteAsJsonAsync(new { status = "ok" });
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
