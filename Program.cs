    using Microsoft.EntityFrameworkCore;
    using TovUmarpeh;
    using Amazon.S3;
    using Amazon.S3.Model;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

builder.Services.AddDbContext<UsersDBContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("UsersDb"), 
    ServerVersion.Parse("8.0.41-mysql")));

// הוספת Controllers עם JsonOptions
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
});
DotNetEnv.Env.Load();

var app = builder.Build();
    app.UseCors("AllowSpecificOrigin");
    //users
    app.MapGet("/users", async (UsersDBContext context) =>
    {
        return await context.UsersTables.ToListAsync();
    });
    app.MapGet("/users/{id}", async (UsersDBContext context, int id) =>
    {
        var user = await context.UsersTables.FindAsync(id);
        if (user is null)
        {
            return Results.NotFound();
        }
        return Results.Ok(user);
    });
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
            Phone = request.Form["Phone"],
            City = request.Form["City"],
            Email = request.Form["Email"],
            BirthDate = request.Form["BirthDate"]
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
            if (request.Form.Files.Count > 0)
            {
                // הנחה שהקבצים יישלחו בסדר ידוע
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

                    // הקצאת ה-URL לשדה המתאים
                    if (i == 0) // קובץ ראשון - תרופות
                    {
                        medicationsUrl = fileUrl;
                    }
                    else if (i == 1) // קובץ שני - הסכם
                    {
                        agreementUrl = fileUrl;
                    }
                    else if (i == 2) // קובץ שלישי - פרטי אישי
                    {
                        personalDetailsUrl = fileUrl;
                    }
                    else if (i == 3) // קובץ רביעי - תעודת זהות
                    {
                        identityUrl = fileUrl;
                    }
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

    //updateUser
    app.MapPut("/users/{id}", async (UsersDBContext context, HttpRequest request, int id) =>
{
    try
    {
        // קריאת נתוני המשתמש מהבקשה
        var user = new UsersTable
        {
            IdNumber = int.Parse(request.Form["IdNumber"]),
            FirstName = request.Form["FirstName"],
            LastName = request.Form["LastName"],
            Address = request.Form["Address"],
            Phone = request.Form["Phone"],
            City = request.Form["City"],
            Email = request.Form["Email"],
            BirthDate = request.Form["BirthDate"],
        };

        if (id != user.IdNumber)
        {
            return Results.BadRequest("User ID mismatch");
        }

        context.Entry(user).State = EntityState.Modified;
        await context.SaveChangesAsync();

        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}");
    }
});

    //login
    app.MapPost("/login", async (UsersDBContext context, LoginRequest loginRequest) =>
{
    var user = await context.UsersTables
        .FirstOrDefaultAsync(u => u.IdNumber == loginRequest.IdNumber && u.Email == loginRequest.Email);

    if (user == null)
    {
        return Results.NotFound("User not found");
    }
    return Results.Ok("Login successful");
});

  
    //activities
  
    app.MapGet("/activity", async (UsersDBContext context) =>
    {
        return await context.Activities.ToListAsync();
    });
    app.MapGet("/activity/{id}", async (UsersDBContext context, int id) =>
    {
        var activity = await context.Activities.FindAsync(id);
        if (activity is null)
        {
            return Results.NotFound();
        }
        return Results.Ok(activity);
    });
    app.MapGet("/enroll", async (UsersDBContext context) =>
    {
        return await context.Enrollments.ToListAsync();
    });
app.MapPost("/enroll", async (UsersDBContext context, Enrollment enrollment) =>
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

    // בדוק אם המכסה מלאה
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