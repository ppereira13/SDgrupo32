using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Servidor.Models;
using Microsoft.AspNetCore.SignalR;
using Servidor.Hubs;

namespace Servidor.Services
{
    public class MessageConsumerService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly MongoDBService _mongoService;
        private readonly IHubContext<WavyStatusHub> _hubContext;
        private const string ExchangeName = "wavy_logs";

        public MessageConsumerService(MongoDBService mongoService, IHubContext<WavyStatusHub> hubContext)
        {
            _mongoService = mongoService;
            _hubContext = hubContext;

            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declarar o exchange
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct);

            // Criar uma fila exclusiva
            var queueName = _channel.QueueDeclare().QueueName;

            // Vincular a fila ao exchange
            _channel.QueueBind(
                queue: queueName,
                exchange: ExchangeName,
                routingKey: "status"); // Vincular apenas para mensagens de status

            // Configurar o consumidor
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var parts = message.Split('|');

                if (parts.Length == 3)
                {
                    var wavyId = parts[0];
                    var dataType = parts[1];
                    var value = parts[2];

                    if (dataType == "status")
                    {
                        await _hubContext.Clients.All.SendAsync("ReceiveWavyStatus", wavyId, value);
                        Console.WriteLine($"[x] Status da WAVY {wavyId} enviado para a dashboard: {value}");
                    }

                    var wavyData = new WavyData
                    {
                        WavyId = wavyId,
                        DataType = dataType,
                        Value = value,
                        Timestamp = DateTime.UtcNow
                    };

                    await _mongoService.SaveDataAsync(wavyData);
                    Console.WriteLine($"[x] Dados salvos: {wavyId} - {dataType} = {value} (Timestamp: {wavyData.Timestamp:yyyy-MM-dd HH:mm:ss})");
                }
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: true,
                consumer: consumer);

            Console.WriteLine("[*] Aguardando mensagens das WAVYs...");
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
} 