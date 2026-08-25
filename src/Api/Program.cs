// Composition root de Bastion: el host único donde se cablean los módulos (§4 del plan
// maestro). La construcción del sistema vive aquí, separada de su uso
// (`principios/clean-architecture.md`); ningún módulo se registra a sí mismo por su cuenta.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

app.Run();
