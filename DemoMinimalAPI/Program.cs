using DemoMinimalAPI.Data;
using DemoMinimalAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MiniValidation;
using NetDevPack.Identity.Data;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MinimalContextDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<NetDevPackAppDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("DemoMinimalAPI")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CriarFornecedor", p => p.RequireClaim("EditarFornecedor"));
    options.AddPolicy("EditarFornecedor", p => p.RequireClaim("EditarFornecedor"));
    options.AddPolicy("ExcluirFornecedor", p => p.RequireClaim("ExcluirFornecedor"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Demo Minimal API",
        Description = "Desenvolvido por Ari C. Maximiano para seu repositório pessoal",
        Contact = new OpenApiContact { Name = "Ari C. Maximiano", Email = "acmaximiano@gmail.com" },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/license/mit/") }

    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Exemplo: Bearer <SEUTOKEN>",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
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
            new string[] { }
        }
    });
});

WebApplication? app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();

app.UseHttpsRedirection();

app.MapGet("/fornecedor", [Authorize] async (MinimalContextDb context) => await context.Fornecedores.ToListAsync())
    .WithName("GetFornecedores")
    .WithTags("Fornecedor");

app.MapGet("/fornecedor/{id:Guid}", async (MinimalContextDb context, Guid id) =>
     await context.Fornecedores
        .AsNoTracking<Fornecedor>()
        .FirstOrDefaultAsync(f => f.Id == id) is Fornecedor fornecedor
                ? Results.Ok(fornecedor)
                : Results.NotFound("Não encontrado")
)
    .Produces<Fornecedor>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithName("GetFornecedorPorId")
    .WithTags("Fornecedor");

app.MapPost("/fornecedor", [Authorize] async (MinimalContextDb context, Fornecedor fornecedor) =>
{
    if (!MiniValidator.TryValidate(fornecedor, out var errors))
        return Results.ValidationProblem(errors);

    context.Fornecedores.Add(fornecedor);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.CreatedAtRoute("GetFornecedorPorId", new { id = fornecedor.Id }, fornecedor)
        : Results.BadRequest("Houve um problema ao salvar o registro");
})
    .Produces<Fornecedor>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status404NotFound)
    .WithName("PostFornecedor")
    .WithTags("Fornecedor");

app.MapPut("/fornecedor", [Authorize] async (MinimalContextDb context, Guid id, Fornecedor fornecedor) =>
{
    var fornecedorPersistencia = await context.Fornecedores.FindAsync(id);

    if (fornecedorPersistencia == null) Results.NotFound();
    if (!MiniValidator.TryValidate(fornecedor, out var errors))
        Results.ValidationProblem(errors);
    if (fornecedorPersistencia.Equals(fornecedor))
        Results.BadRequest("Não foram encontradas alterações no modelo informado");

    context.Fornecedores.Add(fornecedor);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao salvar o registro");
})
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PutFornecedor")
    .WithTags("Fornecedor");


app.MapDelete("/fornecedor", [Authorize] async (MinimalContextDb context, Guid id) =>
{
    var fornecedor = await context.Fornecedores.FindAsync(id);

    if (fornecedor == null) Results.NotFound();
    context.Fornecedores.Remove(fornecedor);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao atender essa solicitação");
})
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status204NoContent)
    .RequireAuthorization("ExcluirFornecedor")
    .WithName("DeleteFornecedor")
    .WithTags("Fornecedor");

app.MapPost("/registro", [AllowAnonymous] async (
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        RegisterUser registerUser) =>
{
    if (registerUser == null) Results.BadRequest("Usuário não informado");
    if (!MiniValidator.TryValidate(registerUser, out var errors))
        Results.ValidationProblem(errors);

    var user = new IdentityUser
    {
        UserName = registerUser.Email,
        Email = registerUser.Email,
        EmailConfirmed = true
    };

    var result = await userManager.CreateAsync(user, registerUser.Password);

    if (!result.Succeeded) Results.BadRequest(result.Errors);

    var jwt = new JwtBuilder()
        .WithUserManager(userManager)
        .WithJwtSettings(appJwtSettings.Value)
        .WithEmail(user.Email)
        .WithJwtClaims()
        .WithUserClaims()
        .WithUserRoles()
        .BuildUserResponse();

    return Results.Ok(jwt);
})
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("RegistrarUsuario")
    .WithTags("Usuario");

app.MapPost("/login", [AllowAnonymous] async (
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings,
    LoginUser loginUser) =>
{
    if (loginUser == null) Results.BadRequest("Usuário não informado");
    if (!MiniValidator.TryValidate(loginUser, out var errors)) Results.ValidationProblem(errors);

    var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, false);

    if (result.IsLockedOut) Results.BadRequest("Usuário bloqueado, contate o administrador");
    if (!result.Succeeded) Results.BadRequest("Usuário ou senha inválidos");

    var jwt = new JwtBuilder()
     .WithUserManager(userManager)
     .WithJwtSettings(appJwtSettings.Value)
     .WithEmail(loginUser.Email)
     .WithJwtClaims()
     .WithUserRoles()
     .WithUserClaims()
     .BuildUserResponse();

    return Results.Ok(jwt);
})
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("LoginUsuario")
    .WithTags("Usuario");

app.Run();