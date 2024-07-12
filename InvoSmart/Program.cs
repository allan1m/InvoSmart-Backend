using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Serialization;
using System.Text;

// The code sets up and configures an ASP.NET Core web application

// Create a new WebApplicationBuilder, used to configure and build the app
var builder = WebApplication.CreateBuilder(args);

// Add services to the dependency injection container

// Add controller support to the application
builder.Services.AddControllers();

// Configure JWT authentication for the application
builder.Services.AddAuthentication(options =>
{
    // Set the default authentication scheme to JWT Bearer
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    // Set the default challenge scheme to JWT Bearer
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Configure JWT Bearer token validation parameters
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Ensure the token issuer is validated
        ValidateIssuer = true,
        // Ensure the token audience is validated
        ValidateAudience = true,
        // Ensure the token lifetime is validated
        ValidateLifetime = true,
        // Ensure the token signing key is validated
        ValidateIssuerSigningKey = true,
        // Set the valid issuer for the token
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        // Set the valid audience for the token
        ValidAudience = builder.Configuration["Jwt:Issuer"],
        // Set the signing key used to validate the token
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// Add support for OpenAPI/Swagger to document the API
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON serialization settings
//JSON Serializer
builder.Services.AddControllers().AddNewtonsoftJson(option =>
option.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore).AddNewtonsoftJson(
    options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());

// Build the application
var app = builder.Build();

//Enable CORS
// Enable Cross-Origin Resource Sharing (CORS) for specified origins
app.UseCors(c => c.AllowAnyHeader().WithOrigins("http://localhost:3000").AllowAnyMethod());

// Enable Swagger middleware to generate API documentation
app.UseSwagger();

// Configure the middleware pipeline for development environment
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Enable Swagger UI in development mode
    app.UseSwaggerUI();
}

// Enable authentication middleware
app.UseAuthentication();

// Enable authorization middleware
app.UseAuthorization();

// Map incoming requests to controller actions
app.MapControllers();

// Run the application
app.Run();
