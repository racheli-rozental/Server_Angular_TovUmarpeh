// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;
// using System.IdentityModel.Tokens.Jwt;
// using System.Security.Claims;
// using System.Text;
// using TovUmarpeh;
// using Amazon.S3;
// using Amazon.S3.Model;
// using System.Text.Json.Serialization;
// using Microsoft.AspNetCore.Authorization;

// var builder = WebApplication.CreateBuilder(args);

// // הגדרת CORS
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowSpecificOrigin",
//         builder => builder.WithOrigins("http://localhost:4200")
//                           .AllowAnyHeader()
//                           .AllowAnyMethod());
// });

// // הגדרת DbContext
// builder.Services.AddDbContext<UsersDBContext>(options =>
//     options.UseMySql(builder.Configuration.GetConnectionString("UsersDb"),
//     ServerVersion.Parse("8.0.41-mysql")));
// DotNetEnv.Env.Load();
// // הוספת Controllers עם JsonOptions
// builder.Services.AddControllers().AddJsonOptions(options =>
// {
//     options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
// });

// // הגדרת Authentication עם JWT
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true,
//             ValidateAudience = true,
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             ValidIssuer = builder.Configuration["Jwt:Issuer"],
//             ValidAudience = builder.Configuration["Jwt:Audience"],
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.")))
//         };
//     });



// var app = builder.Build();

// app.UseCors("AllowSpecificOrigin");
// app.UseAuthentication(); 
// app.UseAuthorization();  

// // Endpoints
// app.MapGet("/users", [Authorize] async (UsersDBContext context) =>
// {
//     return await context.UsersTables.ToListAsync();
// });

// app.MapGet("/users/{id}", [Authorize] async (UsersDBContext context, int id) =>
// {
//     var user = await context.UsersTables.FindAsync(id);
//     if (user is null)
//     {
//         return Results.NotFound();
//     }
//     return Results.Ok(user);
// });

// app.MapPost("/users", [Authorize] async (UsersDBContext context, HttpRequest request) =>
// {
//     using var transaction = await context.Database.BeginTransactionAsync();
//     try
//     {
//         if (!int.TryParse(request.Form["IdNumber"], out var idNumber))
//         {
//             return Results.BadRequest("Invalid IdNumber format.");
//         }

//         var user = new UsersTable
//         {
//             IdNumber = idNumber,
//             FirstName = request.Form["FirstName"],
//             LastName = request.Form["LastName"],
//             Address = request.Form["Address"],
//             Phone = request.Form["Phone"]!,
//             City = request.Form["City"]!,
//             Email = request.Form["Email"]!,
//             BirthDate = request.Form["BirthDate"]!
//         };

//         context.UsersTables.Add(user);

//         var s3Client = new AmazonS3Client(
//             Environment.GetEnvironmentVariable("KEY_ID"),
//             Environment.GetEnvironmentVariable("ACCESS_KEY"),
//             Amazon.RegionEndpoint.USEast1);

//         var medicationsUrl = string.Empty;
//         var agreementUrl = string.Empty;
//         var personalDetailsUrl = string.Empty;
//         var identityUrl = string.Empty;

//         if (request.Form.Files.Count > 0)
//         {
//             for (int i = 0; i < request.Form.Files.Count; i++)
//             {
//                 var file = request.Form.Files[i];

//                 if (file.Length > 5 * 1024 * 1024)
//                 {
//                     return Results.BadRequest($"File {file.FileName} is too large. Maximum size is 5MB.");
//                 }

//                 var uploadRequest = new PutObjectRequest
//                 {
//                     BucketName = "tovumarpeh",
//                     Key = file.FileName,
//                     InputStream = file.OpenReadStream(),
//                     ContentType = file.ContentType
//                 };

//                 var response = await s3Client.PutObjectAsync(uploadRequest);
//                 if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
//                 {
//                     return Results.Problem($"Error uploading file {file.FileName} to S3. Status code: {response.HttpStatusCode}");
//                 }

//                 var fileUrl = $"https://tovumarpeh.s3.amazonaws.com/{file.FileName}";

//                 if (i == 0) medicationsUrl = fileUrl;
//                 else if (i == 1) agreementUrl = fileUrl;
//                 else if (i == 2) personalDetailsUrl = fileUrl;
//                 else if (i == 3) identityUrl = fileUrl;
//             }
//         }

//         var userFile = new UsersFile
//         {
//             IdNumber = idNumber,
//             Medications = medicationsUrl,
//             Agreement = agreementUrl,
//             PersonalDetails = personalDetailsUrl,
//             Identity = identityUrl
//         };

//         context.UsersFiles.Add(userFile);
//         await context.SaveChangesAsync();
//         await transaction.CommitAsync();

//         return Results.Created($"/users/files/{userFile.Id}", new { userFile });
//     }
//     catch (Exception ex)
//     {
//         await transaction.RollbackAsync();
//         return Results.Problem($"An error occurred: {ex.Message}");
//     }
// });

// app.MapPut("/users/{id}", [Authorize] async (UsersDBContext context, HttpRequest request, int id) =>
// {
//     try
//     {
//         var user = new UsersTable
//         {
//             IdNumber = int.Parse(request.Form["IdNumber"]!),
//             FirstName = request.Form["FirstName"]!,
//             LastName = request.Form["LastName"]!,
//             Address = request.Form["Address"],
//             Phone = request.Form["Phone"]!,
//             City = request.Form["City"]!,
//             Email = request.Form["Email"]!,
//             BirthDate = request.Form["BirthDate"]!,
//         };

//         if (id != user.IdNumber)
//         {
//             return Results.BadRequest("User ID mismatch");
//         }

//         context.Entry(user).State = EntityState.Modified;
//         await context.SaveChangesAsync();

//         return Results.NoContent();
//     }
//     catch (Exception ex)
//     {
//         return Results.Problem($"An error occurred: {ex.Message}");
//     }
// });

// app.MapPost("/login", async (UsersDBContext context, LoginRequest loginRequest) =>
// {
//     var user = await context.UsersTables
//         .FirstOrDefaultAsync(u => u.IdNumber == loginRequest.IdNumber && u.Email == loginRequest.Email);

//     if (user == null)
//     {
//         return Results.NotFound("User not found");
//     }

//     var tokenHandler = new JwtSecurityTokenHandler();
// var key = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY") 
//     ?? throw new InvalidOperationException("JWT_KEY is not configured."));
//     var tokenDescriptor = new SecurityTokenDescriptor
//     {
//         Subject = new ClaimsIdentity(new[]
//         {
//             new Claim(ClaimTypes.NameIdentifier, user.IdNumber.ToString()),
//             new Claim(ClaimTypes.Email, user.Email)
//         }),
//         Expires = DateTime.UtcNow.AddHours(1),
//         Issuer = builder.Configuration["Jwt:Issuer"],
//         Audience = builder.Configuration["Jwt:Audience"],
//         SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
//     };

//     var token = tokenHandler.CreateToken(tokenDescriptor);
//     var tokenString = tokenHandler.WriteToken(token);

//     return Results.Ok(new { Token = tokenString });
// });

// app.MapGet("/activity", [Authorize] async (UsersDBContext context) =>
// {
//     return await context.Activities.ToListAsync();
// });

// app.MapGet("/activity/{id}", [Authorize] async (UsersDBContext context, int id) =>
// {
//     var activity = await context.Activities.FindAsync(id);
//     if (activity is null)
//     {
//         return Results.NotFound();
//     }
//     return Results.Ok(activity);
// });

// app.MapPost("/enroll", [Authorize] async (UsersDBContext context, Enrollment enrollment) =>
// {
//     if (enrollment == null)
//     {
//         return Results.BadRequest("ההרשמה לא יכולה להיות ריקה.");
//     }

//     var userExists = await context.UsersTables.AnyAsync(u => u.IdNumber == enrollment.IdNumber);
//     if (!userExists)
//     {
//         return Results.BadRequest("המשתמש לא קיים." + " " + enrollment.IdNumber);
//     }

//     var existingEnrollment = await context.Enrollments
//         .AnyAsync(e => e.IdActivities == enrollment.IdActivities && e.IdNumber == enrollment.IdNumber);

//     if (existingEnrollment)
//     {
//         return Results.BadRequest("המשתמש כבר רשום לפעילות זו.");
//     }

//     var activity = await context.Activities.FindAsync(enrollment.IdActivities);
//     if (activity == null)
//     {
//         return Results.NotFound("הפעילות לא קיימת.");
//     }

//     var registeredUsersCount = await context.Enrollments
//         .CountAsync(e => e.IdActivities == enrollment.IdActivities);

//     if (registeredUsersCount >= activity.Max)
//     {
//         return Results.BadRequest("המכסה לפעילות זו מלאה.");
//     }

//     context.Enrollments.Add(enrollment);
//     await context.SaveChangesAsync();
//     return Results.Created($"/enroll/{enrollment.EnrollmentId}", enrollment);
// });

// app.Run();

// public class LoginRequest
// {
//     public int IdNumber { get; set; }
//     public string? Email { get; set; }
// }
////////////////////////////////////////////////
///

/////////////////////////////////
///
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TovUmarpeh;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// טעינת משתני סביבה
DotNetEnv.Env.Load();

// בדיקת משתנה הסביבה JWT_KEY
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT_KEY is not configured in the environment variables.");
}
Console.WriteLine($"JWT_KEY Loaded: {jwtKey}");

// הגדרת CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("*")
                          .AllowAnyHeader()
                          .AllowAnyMethod());
});

// הגדרת DbContext
builder.Services.AddDbContext<UsersDBContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("UsersDb"),
    ServerVersion.Parse("8.0.41-mysql")));

// הוספת Controllers עם JsonOptions
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
});

// הגדרת Authentication עם JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // לוגים לתהליך האימות
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully.");
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();

// פונקציות API
app.MapGet("/users", [Authorize] async (UsersDBContext context) =>
{
    Console.WriteLine("Accessing /users endpoint");
    return await context.UsersTables.ToListAsync();
});

app.MapGet("/users/{id}", [Authorize] async (UsersDBContext context, int id) =>
{
    Console.WriteLine($"Accessing /users/{id} endpoint");
    var user = await context.UsersTables.FindAsync(id);
    if (user is null)
    {
        return Results.NotFound();
    }
    return Results.Ok(user);
});

app.MapPost("/users", [Authorize] async (UsersDBContext context, HttpRequest request) =>
{
    using var transaction = await context.Database.BeginTransactionAsync();
    try
    {
        if (!int.TryParse(request.Form["IdNumber"], out var idNumber))
        {
            return Results.BadRequest("Invalid IdNumber format.");
        }

        var user = new UsersTable
        {
            IdNumber = idNumber,
            FirstName = request.Form["FirstName"],
            LastName = request.Form["LastName"],
            Address = request.Form["Address"],
            Phone = request.Form["Phone"]!,
            City = request.Form["City"]!,
            Email = request.Form["Email"]!,
            BirthDate = request.Form["BirthDate"]!
        };

        context.UsersTables.Add(user);

        var s3Client = new AmazonS3Client(
            Environment.GetEnvironmentVariable("KEY_ID"),
            Environment.GetEnvironmentVariable("ACCESS_KEY"),
            Amazon.RegionEndpoint.USEast1);

        var medicationsUrl = string.Empty;
        var agreementUrl = string.Empty;
        var personalDetailsUrl = string.Empty;
        var identityUrl = string.Empty;

        if (request.Form.Files.Count > 0)
        {
            for (int i = 0; i < request.Form.Files.Count; i++)
            {
                var file = request.Form.Files[i];

                if (file.Length > 5 * 1024 * 1024)
                {
                    return Results.BadRequest($"File {file.FileName} is too large. Maximum size is 5MB.");
                }

                var uploadRequest = new PutObjectRequest
                {
                    BucketName = "tovumarpeh",
                    Key = file.FileName,
                    InputStream = file.OpenReadStream(),
                    ContentType = file.ContentType
                };

                var response = await s3Client.PutObjectAsync(uploadRequest);
                if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    return Results.Problem($"Error uploading file {file.FileName} to S3. Status code: {response.HttpStatusCode}");
                }

                var fileUrl = $"https://tovumarpeh.s3.amazonaws.com/{file.FileName}";

                if (i == 0) medicationsUrl = fileUrl;
                else if (i == 1) agreementUrl = fileUrl;
                else if (i == 2) personalDetailsUrl = fileUrl;
                else if (i == 3) identityUrl = fileUrl;
            }
        }

        var userFile = new UsersFile
        {
            IdNumber = idNumber,
            Medications = medicationsUrl,
            Agreement = agreementUrl,
            PersonalDetails = personalDetailsUrl,
            Identity = identityUrl
        };

        context.UsersFiles.Add(userFile);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created($"/users/files/{userFile.Id}", new { userFile });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return Results.Problem($"An error occurred: {ex.Message}");
    }
});

app.MapPost("/login", async (UsersDBContext context, LoginRequest loginRequest) =>
{
    Console.WriteLine("Accessing /login endpoint");
    var user = await context.UsersTables
        .FirstOrDefaultAsync(u => u.IdNumber == loginRequest.IdNumber && u.Email == loginRequest.Email);

    if (user == null)
    {
        Console.WriteLine("User not found");
        return Results.NotFound("User not found");
    }

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.IdNumber.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        Issuer = builder.Configuration["Jwt:Issuer"],
        Audience = builder.Configuration["Jwt:Audience"],
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    Console.WriteLine("Token created successfully");
    return Results.Ok(new { Token = tokenString });
});

app.MapGet("/activity", [Authorize] async (UsersDBContext context, HttpContext httpContext) =>
{
    Console.WriteLine($"Accessing /activity endpoint");
    Console.WriteLine($"Authorization Header: {httpContext.Request.Headers["Authorization"]}");
    return await context.Activities.ToListAsync();
});

app.MapPost("/enroll", [Authorize] async (UsersDBContext context, Enrollment enrollment) =>
{
    if (enrollment == null)
    {
        return Results.BadRequest("ההרשמה לא יכולה להיות ריקה.");
    }

    var userExists = await context.UsersTables.AnyAsync(u => u.IdNumber == enrollment.IdNumber);
    if (!userExists)
    {
        return Results.BadRequest("המשתמש לא קיים." + " " + enrollment.IdNumber);
    }

    var existingEnrollment = await context.Enrollments
        .AnyAsync(e => e.IdActivities == enrollment.IdActivities && e.IdNumber == enrollment.IdNumber);

    if (existingEnrollment)
    {
        return Results.BadRequest("המשתמש כבר רשום לפעילות זו.");
    }

    var activity = await context.Activities.FindAsync(enrollment.IdActivities);
    if (activity == null)
    {
        return Results.NotFound("הפעילות לא קיימת.");
    }

    var registeredUsersCount = await context.Enrollments
        .CountAsync(e => e.IdActivities == enrollment.IdActivities);

    if (registeredUsersCount >= activity.Max)
    {
        return Results.BadRequest("המכסה לפעילות זו מלאה.");
    }

    context.Enrollments.Add(enrollment);
    await context.SaveChangesAsync();
    return Results.Created($"/enroll/{enrollment.EnrollmentId}", enrollment);
});

app.Run();

public class LoginRequest
{
    public int IdNumber { get; set; }
    public string? Email { get; set; }
}