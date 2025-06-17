using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using System.Threading;

namespace PreProcessamentoRPC
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Iniciando serviço de pré-processamento...");

            try
            {
                using var service = new PreProcessamentoService();
                
                Console.WriteLine("Pressione CTRL+C para encerrar.");
                var exitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    exitEvent.Set();
                };

                exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro fatal: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }
    }
}
