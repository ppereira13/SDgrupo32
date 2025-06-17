using System;
using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class PreProcessamentoRPCClient : IDisposable
{
    private const string QUEUE_NAME = "preprocessamento_rpc_queue";
    private readonly IConnection connection;
    private readonly IModel channel;
    private readonly string replyQueueName;
    private readonly EventingBasicConsumer consumer;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper = new();

    public PreProcessamentoRPCClient()
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
    }

    public Task<string> PreProcessarDadosAsync(string message)
    {
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

        return tcs.Task;
    }

    public void Dispose()
    {
        connection?.Dispose();
        channel?.Dispose();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Cliente RPC de Pré-processamento");
        Console.WriteLine("Pressione Ctrl+C para sair");

        using var rpcClient = new PreProcessamentoRPCClient();

        // Exemplos de dados em diferentes formatos
        var dadosJSON = "{\"WavyId\":\"WAVY001\",\"TipoDado\":\"temperatura\",\"Valor\":\"25.5\",\"FormatoOrigem\":\"JSON\"}";
        var dadosCSV = "WAVY002,pressao,1013.25,hPa";
        var dadosXML = "<leitura><sensor>WAVY003</sensor><tipo>umidade</tipo><valor>85</valor></leitura>";
        var dadosTexto = "Leitura do sensor WAVY004: profundidade 150m";

        try
        {
            // Testando processamento de JSON
            Console.WriteLine("\nEnviando dados JSON...");
            var respostaJSON = await rpcClient.PreProcessarDadosAsync(dadosJSON);
            Console.WriteLine($"Resposta: {respostaJSON}");

            // Testando processamento de CSV
            Console.WriteLine("\nEnviando dados CSV...");
            var respostaCSV = await rpcClient.PreProcessarDadosAsync(dadosCSV);
            Console.WriteLine($"Resposta: {respostaCSV}");

            // Testando processamento de XML
            Console.WriteLine("\nEnviando dados XML...");
            var respostaXML = await rpcClient.PreProcessarDadosAsync(dadosXML);
            Console.WriteLine($"Resposta: {respostaXML}");

            // Testando processamento de texto
            Console.WriteLine("\nEnviando dados em texto...");
            var respostaTexto = await rpcClient.PreProcessarDadosAsync(dadosTexto);
            Console.WriteLine($"Resposta: {respostaTexto}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro: {e.Message}");
        }

        Console.WriteLine("\nPressione qualquer tecla para sair");
        Console.ReadKey();
    }
}
