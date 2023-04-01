using Persistance.EF.Service;
using Persistance.Memory.Services;
using Stamon.Model;
using Stamon.Services;
using Stamon.Types.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

IConfigurationSection sec = builder.Configuration.GetSection("Monitor");
builder.Services.Configure<StarisConfig>(sec);

builder.Services.AddSingleton<StamonService>();

string statPath = builder.Configuration.GetValue<string>("Monitor:EF");

builder.Services.AddSingleton<IStamonMemoryPersistance, PersistanceService>();

builder.Services.AddSingleton<IStamonDatabasePersistance, EfStatisticService>(
    s => new EfStatisticService(statPath));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
