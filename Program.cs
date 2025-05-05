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
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
var builder = WebApplication.CreateBuilder(args);


DotNetEnv.Env.Load();


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
        builder => builder.WithOrigins("http://localhost:4200","https://users-tuvumarpeh.onrender.com") 
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

// app.MapPost("/users1",  async (UsersDBContext context, HttpRequest request) =>
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
app.MapPost("/users", async (UsersDBContext context, HttpRequest request) =>
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

        string medicationsUrl = "", agreementUrl = "", personalDetailsUrl = "", identityUrl = "";
        if (request.Form.Files.Count > 0)
        {
            foreach (var file in request.Form.Files)
            {
                if (file.Length > 5 * 1024 * 1024)
                {
                    return Results.BadRequest($"File {file.FileName} is too large. Maximum size is 5MB.");
                }

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // שמירת תוכן הזרם במערך
                byte[] fileBytes = memoryStream.ToArray();

                string extractedText;
                try
                {
                    extractedText = await SomeAIService.ExtractTextAsync(new MemoryStream(fileBytes));
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Failed to extract text from file: {ex.Message}");
                }

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    return Results.BadRequest($"Could not extract text from file {file.FileName}.");
                }

                var documentType = DocumentTypeDetector.DetectDocumentType(extractedText);

                if (string.IsNullOrEmpty(documentType))
                {
                    return Results.BadRequest($"Could not detect document type for file {file.FileName}.");
                }

                var uniqueId = Guid.NewGuid().ToString();
                var newFileName = $"{idNumber}_{documentType}_{uniqueId}.pdf";

                // שימוש בזרם חדש עם התוכן
                using var uploadStream = new MemoryStream(fileBytes);
                var uploadRequest = new PutObjectRequest
                {
                    BucketName = "tovumarpeh",
                    Key = newFileName,
                    InputStream = uploadStream,
                    ContentType = file.ContentType
                };

                var response = await s3Client.PutObjectAsync(uploadRequest);

                if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    return Results.Problem($"Error uploading file {newFileName} to S3. Status code: {response.HttpStatusCode}");
                }

                var fileUrl = $"https://tovumarpeh.s3.amazonaws.com/{newFileName}";

                switch (documentType.ToLower())
                {
                    case "medications":
                        medicationsUrl = fileUrl;
                        break;
                    case "agreement":
                        agreementUrl = fileUrl;
                        break;
                    case "personaldetails":
                        personalDetailsUrl = fileUrl;
                        break;
                    case "identity":
                        identityUrl = fileUrl;
                        break;
                }
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


app.MapGet("/files/{fileName}", async (UsersDBContext context, HttpContext httpContext, string fileName) =>
{
    var s3Client = new AmazonS3Client(
        Environment.GetEnvironmentVariable("KEY_ID"),
        Environment.GetEnvironmentVariable("ACCESS_KEY"),
        Amazon.RegionEndpoint.USEast1);

    var request = new GetObjectRequest
    {
        BucketName = "tovumarpeh",
        Key = fileName
    };

    using var response = await s3Client.GetObjectAsync(request);
    using var responseStream = response.ResponseStream;
    using var memoryStream = new MemoryStream();
    await responseStream.CopyToAsync(memoryStream);

    return Results.File(memoryStream.ToArray(), response.Headers["Content-Type"], fileName);
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
app.MapGet("/activity/{id}", [Authorize] async (int id, UsersDBContext context, HttpContext httpContext) =>
{
    Console.WriteLine($"Accessing /activity/{id} endpoint");
    Console.WriteLine($"Authorization Header: {httpContext.Request.Headers["Authorization"]}");

    var activity = await context.Activities.FindAsync(id);

    if (activity == null)
    {
        return Results.NotFound(); 
    }

    return Results.Ok(activity); 
});


app.Run();

public static class SomeAIService
{
    private static readonly string endpoint = Environment.GetEnvironmentVariable("AZURE_FORM_RECOGNIZER_ENDPOINT");
    private static readonly string apiKey = Environment.GetEnvironmentVariable("AZURE_FORM_RECOGNIZER_API_KEY");

    public static async Task<string> ExtractTextAsync(Stream fileStream)
    {
        var credential = new AzureKeyCredential(apiKey);
        var client = new DocumentAnalysisClient(new Uri(endpoint), credential);

        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", fileStream);

        var result = operation.Value;

        var extractedText = new StringBuilder();

        foreach (var page in result.Pages)
        {
            foreach (var line in page.Lines)
            {
                extractedText.AppendLine(line.Content);
            }
        }

        return extractedText.ToString();
    }
}
public static class DocumentTypeDetector
{
    public static string DetectDocumentType(string text)
    {
        if (text.Contains("טופס תרופות") || text.Contains("medications", StringComparison.OrdinalIgnoreCase))
        {
            return "Medications";
        }
        if (text.Contains("טופס הסכמה") || text.Contains("agreement", StringComparison.OrdinalIgnoreCase))
        {
            return "Agreement";
        }
        if (text.Contains("פרטים אישיים") || text.Contains("personaldetails", StringComparison.OrdinalIgnoreCase))
        {
            return "PersonalDetails";
        }
        if (text.Contains("ספח תעודת זהות") || text.Contains("identity", StringComparison.OrdinalIgnoreCase))
        {
            return "Identity";
        }
        return "";
    }
}

public class LoginRequest
{
    public int IdNumber { get; set; }
    public string? Email { get; set; }
}
