using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Agregador
{
    class Aggregator : IDisposable
    {
        private TcpListener listener;
        private readonly ConfigLoader configLoader;
        private readonly DataPreProcessor dataPreProcessor;
        private bool isRunning;
        private readonly ManualResetEvent connectionHandler = new ManualResetEvent(false);

        public Aggregator(string ipAddress, int port, string wavyConfigPath, string dataTypesConfigFolder)
        {
            try
            {
                listener = new TcpListener(IPAddress.Parse(ipAddress), port);
                configLoader = new ConfigLoader(wavyConfigPath, dataTypesConfigFolder);
                dataPreProcessor = new DataPreProcessor(configLoader);
                isRunning = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inicializar o Agregador: {ex.Message}");
                throw;
            }
        }

        public static void LogWithSpacing(string message)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(message);
            Console.WriteLine();
        }

        public void Start()
        {
            try
            {
                listener.Start();
                isRunning = true;
                Console.WriteLine("Agregador iniciado. Aguardando conexões...");

                while (isRunning)
                {
                    connectionHandler.Reset();
                    listener.BeginAcceptTcpClient(HandleClientConnectionCallback, null);
                    connectionHandler.WaitOne();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar o Agregador: {ex.Message}");
            }
            finally
            {
                Stop();
            }
        }

        private void HandleClientConnectionCallback(IAsyncResult ar)
        {
            try
            {
                TcpClient client = listener.EndAcceptTcpClient(ar);
                connectionHandler.Set();

                Thread clientThread = new Thread(() => HandleClientConnection(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao aceitar conexão do cliente: {ex.Message}");
                connectionHandler.Set();
            }
        }

        private void HandleClientConnection(TcpClient client)
        {
            try
            {
                Console.WriteLine($"Cliente conectado: {((IPEndPoint)client.Client.RemoteEndPoint)?.Address}");

                NetworkStream? stream = client.GetStream();
                if (stream == null)
                {
                    Console.WriteLine("Não foi possível obter o stream do cliente.");
                    client.Close();
                    return;
                }
                byte[] buffer = new byte[4096];

                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string initialMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                string[] messageParts = initialMessage.Split('|');
                if (messageParts.Length < 2 || messageParts[0] != "CONNECT")
                {
                    SendResponse(stream, "ERROR|Protocolo inválido");
                    client.Close();
                    return;
                }

                string wavyId = messageParts[1];
                Console.WriteLine($"WAVY {wavyId} conectada");

                SendResponse(stream, "CONNECTED|OK");

                while (client.Connected)
                {
                    try
                    {
                        if (!stream.DataAvailable)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        ProcessMessage(wavyId, message, stream);
                    }
                    catch (IOException) { break; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar mensagem de {wavyId}: {ex.Message}");
                        try { SendResponse(stream, "ERROR|Erro interno"); } catch { break; }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro na conexão do cliente: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private void SendResponse(NetworkStream stream, string response)
        {
            try
            {
                byte[] responseData = Encoding.UTF8.GetBytes(response);
                stream.Write(responseData, 0, responseData.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar resposta: {ex.Message}");
            }
        }

        private async void ProcessMessage(string wavyIdFromConnect, string message, NetworkStream stream)
        {
            try
            {
                string[] messageParts = message.Split('|');
                if (messageParts.Length == 0)
                {
                    SendResponse(stream, "ERROR|Mensagem vazia");
                    return;
                }

                string command = messageParts[0];

                switch (command)
                {
                    case "ASSOC":
                        if (messageParts.Length >= 2)
                        {
                            string wavyId = messageParts[1];
                            Console.WriteLine($"WAVY {wavyId} associada.");
                            SendResponse(stream, "ASSOC_OK");
                        }
                        else SendResponse(stream, "ERROR|Formato inválido para ASSOC");
                        break;

                    case "REG_DATA":
                        if (messageParts.Length >= 3)
                        {
                            string wavyId = messageParts[1];
                            string tipos = messageParts[2];
                            new WavyStatusUpdater(configLoader).RegisterWavy(wavyId, tipos);
                            SendResponse(stream, "REG_DATA_OK");
                        }
                        else SendResponse(stream, "ERROR|Formato inválido para REG_DATA");
                        break;

                    case "STATUS":
                        if (messageParts.Length >= 3)
                        {
                            string wavyId = messageParts[1];
                            string status = messageParts[2];
                            new WavyStatusUpdater(configLoader).UpdateStatus(wavyId, status);
                            SendResponse(stream, "STATUS_OK");
                        }
                        else SendResponse(stream, "ERROR|Formato inválido para STATUS");
                        break;

                    case "DATA":
                        if (messageParts.Length >= 4)
                        {
                            string wavyId = messageParts[1];
                            string dataType = messageParts[2];
                            string value = messageParts[3];
                            
                            try
                            {
                                bool success = await dataPreProcessor.ProcessData(wavyId, dataType, value);
                                SendResponse(stream, success ? "DATA_OK" : $"ERROR|Falha ao processar dados para {dataType}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar dados via RPC: {ex.Message}");
                                SendResponse(stream, $"ERROR|Falha no processamento RPC: {ex.Message}");
                            }
                        }
                        else SendResponse(stream, "ERROR|Formato inválido para DATA");
                        break;

                    case "CLOSE":
                        if (messageParts.Length >= 2)
                        {
                            string wavyId = messageParts[1];
                            new WavyStatusUpdater(configLoader).UpdateStatus(wavyId, "desativada");
                            SendResponse(stream, "CLOSE_OK");
                        }
                        else SendResponse(stream, "ERROR|Formato inválido para CLOSE");
                        break;

                    default:
                        SendResponse(stream, $"ERROR|Comando desconhecido: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mensagem: {ex.Message}");
                SendResponse(stream, "ERROR|Erro interno");
            }
        }

        public void Stop()
        {
            try
            {
                isRunning = false;
                connectionHandler.Set();
                listener.Stop();
                Console.WriteLine("Agregador parado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parar o Agregador: {ex.Message}");
            }
        }

        public void ReloadConfigurations()
        {
            configLoader.LoadConfigurations();
        }

        public void Dispose()
        {
            try
            {
                Stop();
                dataPreProcessor.Dispose();
                connectionHandler.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao liberar recursos do Agregador: {ex.Message}");
            }
        }
    }
}
