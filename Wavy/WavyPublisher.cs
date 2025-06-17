using System;
using System.Text;
using RabbitMQ.Client;

namespace Wavy
{
    public class WavyPublisher : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private const string ExchangeName = "wavy_logs";

        public WavyPublisher()
        {
            try
            {
                var factory = new ConnectionFactory() { HostName = "localhost" };
                
                // Tentar estabelecer a conexão com retry
                int maxRetries = 3;
                int currentTry = 0;
                Exception lastException = null;

                while (currentTry < maxRetries)
                {
                    try
                    {
                        _connection = factory.CreateConnection();
                        _channel = _connection.CreateModel();

                        // Declarar o exchange do tipo direct
                        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct);
                        
                        Console.WriteLine("Conexão com RabbitMQ estabelecida com sucesso!");
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        currentTry++;
                        
                        if (currentTry < maxRetries)
                        {
                            Console.WriteLine($"Tentativa {currentTry} falhou. Tentando novamente em 2 segundos...");
                            System.Threading.Thread.Sleep(2000);
                        }
                    }
                }

                throw new Exception($"Não foi possível conectar ao RabbitMQ após {maxRetries} tentativas.", lastException);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inicializar WavyPublisher: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Causa: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        public void PublishData(string wavyId, string dataType, string value)
        {
            try
            {
                var message = $"{wavyId}|{dataType}|{value}";
                var body = Encoding.UTF8.GetBytes(message);

                _channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: dataType,
                    basicProperties: null,
                    body: body);

                Console.WriteLine($"[x] WAVY {wavyId} publicou: {dataType}={value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao publicar dados: {ex.Message}");
                throw;
            }
        }

        public void PublishStatus(string wavyId, string status)
        {
            try
            {
                var message = $"{wavyId}|status|{status}";
                var body = Encoding.UTF8.GetBytes(message);

                _channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: "status",
                    basicProperties: null,
                    body: body);

                Console.WriteLine($"[x] WAVY {wavyId} atualizou status: {status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao publicar status: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao fechar conexões: {ex.Message}");
            }
        }
    }
} 