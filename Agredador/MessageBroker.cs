using System;
using System.Text;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Agregador
{
    public class MessageBroker : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private const string ExchangeName = "wavy_logs";
        private string _queueName;

        public MessageBroker()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declarar o exchange do tipo direct
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct);
            
            // Criar uma fila exclusiva e temporária
            _queueName = _channel.QueueDeclare().QueueName;
        }

        public void PublishMessage(string wavyId, string dataType, string value)
        {
            var message = $"{wavyId}|{dataType}|{value}";
            var body = Encoding.UTF8.GetBytes(message);

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: dataType,
                basicProperties: null,
                body: body);

            Console.WriteLine($"[x] Publicada mensagem: {message}");
        }

        public void Subscribe(Action<string, string, string> messageHandler, IEnumerable<string> dataTypes)
        {
            // Desvincular todas as binding keys anteriores
            try
            {
                foreach (var dataType in dataTypes)
                {
                    _channel.QueueUnbind(_queueName, ExchangeName, dataType);
                }
            }
            catch { /* Ignora se não houver bindings anteriores */ }

            // Vincular a fila ao exchange com as routing keys específicas
            foreach (var dataType in dataTypes)
            {
                _channel.QueueBind(
                    queue: _queueName,
                    exchange: ExchangeName,
                    routingKey: dataType);
                Console.WriteLine($"[*] Inscrito para receber dados do tipo: {dataType}");
            }

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var parts = message.Split('|');

                if (parts.Length == 3)
                {
                    var wavyId = parts[0];
                    var dataType = parts[1];
                    var value = parts[2];
                    messageHandler(wavyId, dataType, value);
                }
            };

            _channel.BasicConsume(
                queue: _queueName,
                autoAck: true,
                consumer: consumer);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
} 