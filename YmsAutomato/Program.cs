using YmsAutomato.Models;
using YmsAutomato.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Adiciona CORS para permitir acesso do frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Registra o MotorLogistico como Singleton para manter o estado do autômato
builder.Services.AddSingleton<MotorLogistico>();

var app = builder.Build();

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoint para processar eventos
app.MapPost("/processar-evento", ([FromBody] RequisicaoEvento req, MotorLogistico motor) =>
{
    var (sucesso, resposta) = motor.ProcessarEvento(req.Evento, req.Peso);
    
    if (!sucesso)
    {
        return Results.BadRequest(resposta);
    }
    
    return Results.Ok(resposta);
}).DisableAntiforgery();

// Endpoint para obter estado atual
app.MapGet("/estado", (MotorLogistico motor) => motor.ObterEstadoAtual());

// Endpoint para resetar o simulador
app.MapPost("/reset", (MotorLogistico motor) => {
    motor.Reset();
    return Results.Ok(motor.ObterEstadoAtual());
}).DisableAntiforgery();

app.Run();
