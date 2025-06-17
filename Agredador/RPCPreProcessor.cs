using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json.Serialization;

namespace Agregador
{
    public class RPCPreProcessor : IDisposable
    {
        private const string QUEUE_NAME = "preprocessamento_rpc_queue";
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string replyQueueName;
        private readonly EventingBasicConsumer consumer;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper = new();

        public RPCPreProcessor()
        {
            try
            {
                var factory = new ConnectionFactory() { HostName = "localhost" };
                connection = factory.CreateConnection();
                channel = connection.CreateModel();
                replyQueueName = channel.QueueDeclare().QueueName;
                consumer = new EventingBasicConsumer(channel);

                consumer.Received += (model, ea) =>
                {
                    if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs))
                        return;

                    var body = ea.Body.ToArray();
                    var response = Encoding.UTF8.GetString(body);
                    tcs.TrySetResult(response);
                };

                channel.BasicConsume(consumer: consumer,
                                   queue: replyQueueName,
                                   autoAck: true);

                Console.WriteLine("Cliente RPC de Pré-processamento inicializado com sucesso");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inicializar cliente RPC: {ex.Message}");
                throw;
            }
        }

        public async Task<string> PreProcessarDadosAsync(string wavyId, string dataType, string value)
        {
            try
            {
                // Criar objeto de dados
                var dados = new {
                    WavyId = wavyId,
                    TipoDado = dataType,
                    Valor = value,
                    FormatoOrigem = "JSON"
                };

                // Serializar para JSON
                var options = new System.Text.Json.JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                string message = System.Text.Json.JsonSerializer.Serialize(dados, options);

                // Enviar para pré-processamento
                IBasicProperties props = channel.CreateBasicProperties();
                var correlationId = Guid.NewGuid().ToString();
                props.CorrelationId = correlationId;
                props.ReplyTo = replyQueueName;

                var messageBytes = Encoding.UTF8.GetBytes(message);
                var tcs = new TaskCompletionSource<string>();
                callbackMapper.TryAdd(correlationId, tcs);

                channel.BasicPublish(exchange: "",
                                   routingKey: QUEUE_NAME,
                                   basicProperties: props,
                                   body: messageBytes);

                // Aguardar resposta com timeout de 5 segundos
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));
                
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    throw new TimeoutException("Timeout ao aguardar resposta do serviço de pré-processamento");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao pré-processar dados via RPC: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                channel?.Close();
                connection?.Close();
                channel?.Dispose();
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao fechar conexões RPC: {ex.Message}");
            }
        }
    }
} 