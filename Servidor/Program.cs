using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Servidor.Hubs;
using Servidor.Services;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços ao container
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// Configurar MongoDB
string mongoConnectionString = "mongodb://localhost:27017";
builder.Services.AddSingleton(new MongoDBService(mongoConnectionString));

// Registrar serviços adicionais
builder.Services.AddSingleton<DataAnalysisService>();
builder.Services.AddSingleton<MessageConsumerService>();

// Adicionar suporte a SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Inicializar o consumidor de mensagens
var messageConsumer = app.Services.GetRequiredService<MessageConsumerService>();

// Configurar o pipeline de requisições HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Mapear o Hub do SignalR
app.MapHub<WavyStatusHub>("/wavyStatusHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();