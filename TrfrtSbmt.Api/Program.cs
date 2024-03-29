using System.Runtime.CompilerServices;
using System.Security.Claims;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.SimpleEmailV2;
using MediatR;
using Microsoft.OpenApi.Models;
using TrfrtSbmt.Api.Features.Festivals;
using TrfrtSbmt.Api.Features.Forts;
using TrfrtSbmt.Api.Features.Labels;
using TrfrtSbmt.Api.Features.Submissions;
using TrfrtSbmt.Api.Features.Voting;
using TrfrtSbmt.Api.Utilities;
using TrfrtSbmt.Api.Utils.DiscordWebhooks;

[assembly: InternalsVisibleTo("TrfrtSbmt.Tests")]

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

// add typed appsettings file
var appSettings = new AppSettings(builder.Configuration);
builder.Services.AddSingleton(appSettings);

// Add other services to the container.
builder.Services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient(s =>
{
    IHttpContextAccessor contextAccessor = s.GetService<IHttpContextAccessor>();
    ClaimsPrincipal? user = contextAccessor?.HttpContext?.User;
    return user ?? throw new System.Exception("User not resolved");
});
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddAWSService<IAmazonS3>(new AWSOptions
{
    Region = RegionEndpoint.USWest2
});
builder.Services.AddAWSService<IAmazonSimpleEmailServiceV2>(new AWSOptions
{
    Region = RegionEndpoint.USWest2
});
builder.Services.AddScoped<IDiscordWebhookClient>(sp => {
    var uri = appSettings.DiscordWebhookUrl;
if (string.IsNullOrEmpty(uri) || uri == "#{DISCORDWEBHOOKURL}#")
        return new DiscordWebhookClient
        {
            Uri = null
        }; ;
    return new DiscordWebhookClient
    {
        Uri = new Uri(uri)
    };
});
builder.Services.AddMediatR(typeof(Program));

// Add AWS Lambda support.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// add swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts => 
{
    opts.SwaggerDoc("v1", new OpenApiInfo()
    {
        Title = "Submit API",
        Version = "v1"
    });
    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });

    //opts.OperationFilter<SwaggerCustomizations.CustomHeaderSwaggerAttribute>();
});


// configure auth
builder.Services.AddAuthorization(opts =>
{
    // add auth policy called admin
    opts.AddPolicy("admin", policy => policy.RequireClaim("cognito:groups", "admin"));
    opts.AddPolicy("voter", policy => 
    {
        policy.RequireAssertion(context =>
        {
            var user = context.User;
            var groups = user.Claims.Where(c => c.Type == "cognito:groups").Select(c => c.Value);
            return groups.Contains("admin") || groups.Contains("voter");
        });
    });
})
    .ConfigureCognitoAuth(appSettings);

var app = builder.Build();
app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod())
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization();

app.UseSwagger();
app.UseMiddleware<DiscordExceptionLogger>();
app.UseSwaggerUI(options =>
{
    var url = "swagger/v1/swagger.json";
    options.SwaggerEndpoint(url, "v1");
    options.RoutePrefix = string.Empty;
});

// health check
app.MapGet("/healthcheck", () => "Submit Api!")
    .RequireAuthorization();

// festivals
app.MapGet("/festivals", async (bool activeOnly, bool? submissionsOpen, int pageSize, string? paginationKey, [FromServices] IMediator mediator) 
    => await mediator.Send(new ListFestivalsQuery(activeOnly, submissionsOpen ?? false, pageSize, paginationKey)))
    .RequireAuthorization();

app.MapPost("/festivals", async (AddFestivalCommand command, [FromServices] IMediator mediator)
    => await mediator.Send(command))
    .RequireAuthorization("admin");
    

app.MapDelete("/festivals/{festivalId}", async (string festivalId, [FromServices] IMediator mediator)
    => await mediator.Send(new DeleteFestivalCommand(festivalId)))
    .RequireAuthorization("admin");

// forts
app.MapGet("/festivals/{festivalId}/forts", async (string festivalId, int pageSize, string ? paginationKey,[FromServices] IMediator mediator)
    => await mediator.Send(new ListForts.ListFortsQuery(festivalId, pageSize, paginationKey)))
    .RequireAuthorization();

app.MapPost("/festivals/{festivalId}/forts", async (string festivalId, AddFort.AddFortCommand command, [FromServices] IMediator mediator) =>    
{
    command.FestivalId = festivalId;
    return await mediator.Send(command);
})
    .RequireAuthorization("admin");

app.MapDelete("/festivals/{festivalId}/forts/{fortId}", async (string festivalId, string fortId, [FromServices] IMediator mediator)
    => await mediator.Send(new DeleteFort.DeleteFortCommand(fortId)))
    .RequireAuthorization("admin");

// submissions
app.MapPost("/festivals/{festivalId}/forts/{fortId}/submissions", async (string festivalId, string fortId, AddSubmission.AddSubmissionCommand command, [FromServices] IMediator mediator) => 
{
    command.FestivalId = festivalId;
    command.FortId = fortId;
    return await mediator.Send(command); 
})
    .RequireAuthorization();

app.MapGet("/festivals/{festivalId}/forts/{fortId}/submissions", async (string festivalId, string fortId, int pageSize, string? createdBy, string? paginationKey, [FromServices] IMediator mediator)
    => await mediator.Send(new ListSubmissions.ListSubmissionsQuery(festivalId, fortId, pageSize, createdBy, paginationKey)))
    .RequireAuthorization();

app.MapGet("/festivals/{festivalId}/submissions", async (string festivalId, int pageSize, string? createdBy, string? paginationKey, [FromServices] IMediator mediator)
    => await mediator.Send(new ListSubmissions.ListSubmissionsQuery(festivalId, null, pageSize, createdBy, paginationKey)))
    .RequireAuthorization();

app.MapGet("/festivals/{festivalId}/forts/{fortId}/submissions/{submissionId}", async (string festivalId, string fortId, string submissionId, [FromServices] IMediator mediator)
    => await mediator.Send(new GetSubmission.GetSubmissionQuery(festivalId, fortId, submissionId)))
    .RequireAuthorization();

app.MapPost("/festivals/{festivalId}/forts/{fortId}/submissions/{submissionId}/review", async (string festivalId, string fortId, string submissionId, ReviewSubmission.ReviewSubmissionCommand command, [FromServices] IMediator mediator)
    => 
{
    command.FestivalId = festivalId;
    command.FortId = fortId;
    command.SubmissionId = submissionId;
    await mediator.Send(command);
})
    .RequireAuthorization("admin");

app.MapGet("/photo-upload-url", async (string fileName, string fileType, [FromServices] IMediator mediator) 
    => await mediator.Send(new GetUploadUrl.Query(fileName, fileType)));


// labels
app.MapPost("/festivals/{festivalId}/labels", async (string festivalId, AddLabel.AddLabelCommand command, [FromServices] IMediator mediator) =>
{
    command.FestivalId = festivalId;
    return await mediator.Send(command);
})
    .RequireAuthorization("voter");

app.MapGet("/festivals/{festivalId}/labels/{labelId}", async (string festivalId, string labelId, int pageSize, string? paginationKey, [FromServices] IMediator mediator)
    => await mediator.Send(new GetLabel.GetLabelQuery(labelId, pageSize, paginationKey)))
    .RequireAuthorization("voter");

app.MapGet("/festivals/{festivalId}/labels", async (string festivalId, int pageSize, string? paginationKey, [FromServices] IMediator mediator) 
    => await mediator.Send(new ListLabels.ListLabelsQuery(festivalId, pageSize, paginationKey)))
.RequireAuthorization("voter");

app.MapDelete("/festivals/{festivalId}/labels/{labelId}", async (string festivalId, string labelId, string? submissionId, [FromServices] IMediator mediator) =>
{
    if(submissionId == null)
    {
        await mediator.Send(new DeleteLabel.DeleteLabelCommand(labelId));
    }
    else
    {
        await mediator.Send(new RemoveLabel.RemoveLabelCommand(labelId, submissionId));
    }
}).RequireAuthorization("admin");

// voting
app.MapGet("/votes", async ([FromServices] IMediator mediator) =>
{
    return await mediator.Send(new GetMyVotes.GetMyVotesQuery());
}).RequireAuthorization("voter");

app.MapPost("/votes", async (VoteOnSubmission.VoteOnSubmissionCommand command, [FromServices] IMediator mediator) => 
{
    await mediator.Send(command);
}).RequireAuthorization("voter");

app.MapGet("/festivals/{festivalId}/forts/{fortId}/vote-tally", async (string festivalId, string fortId, GetVoteTally.Sort sort, int pageSize, string? paginationKey, [FromServices] IMediator mediator) =>
{
    return await mediator.Send(new GetVoteTally.GetVoteTallyQuery(festivalId, fortId, sort, pageSize, paginationKey));
}).RequireAuthorization("admin");

app.MapPost("/vote-tally", async ([FromServices] IMediator mediator) =>
{
    return await mediator.Send(new UpdateVoteTally.UpdateVoteTallyCommand());
}).RequireAuthorization("admin");

app.Run();
