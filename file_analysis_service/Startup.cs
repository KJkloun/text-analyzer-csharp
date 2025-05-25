using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FileAnalysisService.Services.Validation;
using FileAnalysisService.Services;

namespace FileAnalysisService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            
            // Добавляем HttpClient для взаимодействия с сервисом хранения файлов
            services.AddHttpClient("FileStoringService", client =>
            {
                var baseUrl = Configuration["ServiceUrls:FileStoringService"] ?? "http://file-storing-service:8001";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            
            // Добавляем фабрику HttpClient
            services.AddHttpClient();
            
            // Добавляем сервисы для анализа файлов
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IPlagiarismService, PlagiarismService>();
            services.AddScoped<IWordCloudService, WordCloudService>();
            services.AddScoped<IFileValidationService, FileValidationService>();
            
            // Добавляем Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo 
                { 
                    Title = "File Analysis Service", 
                    Version = "v1",
                    Description = "Service for text file analysis including statistics, plagiarism detection, and word cloud generation"
                });
            });
            
            // Добавляем CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            // Добавляем поддержку статических файлов
            app.UseStaticFiles();
            
            // Включаем Swagger для всех режимов (как в File Storing Service)
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "File Analysis Service v1");
                c.RoutePrefix = "swagger"; // Swagger UI по адресу /swagger/index.html
                c.DocumentTitle = "File Analysis Service API Documentation";
                c.EnableDeepLinking();
                c.DisplayRequestDuration();
            });
            
            app.UseRouting();
            app.UseCors("AllowAll");
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                
                // Health check endpoint
                endpoints.MapGet("/health", async context =>
                {
                    await context.Response.WriteAsJsonAsync(new { ok = true });
                });
            });

            // Создаем директорию для результатов анализа, если она не существует
            var resultsDir = Configuration["ResultsDir"] ?? Path.Combine(env.ContentRootPath, "results");
            if (!Directory.Exists(resultsDir))
            {
                Directory.CreateDirectory(resultsDir);
            }
        }
    }
}
