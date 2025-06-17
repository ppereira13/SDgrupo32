using System;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;

namespace Wavy
{
    class Program
    {
        private static readonly object consoleLock = new object();
        private static bool executando = true;
        private static Dictionary<string, WavyDevice> dispositivos = new Dictionary<string, WavyDevice>();
        private static WavyPublisher publisher = new WavyPublisher();

        class WavyDevice
        {
            public string ID { get; set; }
            public bool IsConnected { get; set; }
            public Thread? Thread { get; set; }
            public bool IsRunning { get; set; }

            public WavyDevice(string id)
            {
                ID = id;
                IsConnected = false;
                IsRunning = false;
                Thread = null;
            }
        }

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("\n=== WAVY - Sistema de Monitoramento Oceânico ===\n");

                // Solicitar número de Wavys ao usuário
                Console.Write("Digite o número de dispositivos Wavy que deseja criar: ");
                if (!int.TryParse(Console.ReadLine()?.Trim(), out int numWavys) || numWavys <= 0)
                {
                    Console.WriteLine("Número inválido. O programa será encerrado.");
                    return;
                }

                // Criar os IDs das Wavys dinamicamente
                for (int i = 1; i <= numWavys; i++)
                {
                    string wavyId = $"WAVY{i:D3}"; // Formato: WAVY001, WAVY002, etc.
                    dispositivos[wavyId] = new WavyDevice(wavyId);
                }

                Console.WriteLine($"\n{numWavys} dispositivos Wavy foram criados com sucesso!\n");

                while (executando)
                {
                    MostrarMenu();
                    string? opcao = Console.ReadLine()?.Trim().ToLower();

                    switch (opcao)
                    {
                        case "1":
                            IniciarDispositivo();
                            break;
                        case "2":
                            PararDispositivo();
                            break;
                        case "3":
                            ListarDispositivos();
                            break;
                        case "4":
                            EnviarDadoManual();
                            break;
                        case "5":
                            IniciarTodasWavys();
                            break;
                        case "6":
                            PararTodasWavys();
                            break;
                        case "7":
                            executando = false;
                            break;
                        default:
                            Console.WriteLine("\nOpção inválida. Tente novamente.");
                            break;
                    }
                }

                // Encerrar todos os dispositivos ativos
                foreach (var deviceItem in dispositivos.Values)
                {
                    if (deviceItem.IsRunning)
                    {
                        deviceItem.IsRunning = false;
                        deviceItem.Thread?.Join();
                    }
                }

                publisher.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErro fatal: {ex.Message}");
            }
        }

        static void MostrarMenu()
        {
            Console.WriteLine("\nMenu Principal:");
            Console.WriteLine("1. Iniciar dispositivo Wavy");
            Console.WriteLine("2. Parar dispositivo Wavy");
            Console.WriteLine("3. Listar dispositivos");
            Console.WriteLine("4. Enviar dado manual");
            Console.WriteLine("5. Iniciar todas as Wavys");
            Console.WriteLine("6. Parar todas as Wavys");
            Console.WriteLine("7. Sair");
            Console.Write("\nEscolha uma opção: ");
        }

        static void IniciarDispositivo()
        {
            Console.WriteLine("\nDispositivos Wavy disponíveis:");
            var wavysDisponiveis = new List<string>();
            int contador = 1;

            foreach (var dispositivo in dispositivos)
            {
                if (!dispositivo.Value.IsRunning)
                {
                    Console.WriteLine($"{contador}. {dispositivo.Key}");
                    wavysDisponiveis.Add(dispositivo.Key);
                    contador++;
                }
            }

            if (wavysDisponiveis.Count == 0)
            {
                Console.WriteLine("Não há dispositivos disponíveis para iniciar.");
                return;
            }

            Console.Write($"\nDigite o número (1-{wavysDisponiveis.Count}): ");
            string? opcao = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(opcao) || !int.TryParse(opcao, out int escolha) || 
                escolha < 1 || escolha > wavysDisponiveis.Count)
            {
                Console.WriteLine("\nOpção inválida!");
                return;
            }

            string wavyID = wavysDisponiveis[escolha - 1];
            var deviceToStart = dispositivos[wavyID];

            deviceToStart.IsRunning = true;
            deviceToStart.Thread = new Thread(() => IniciarWavy(wavyID, "acel,gyro,status,hidrofone,transdutor,camera"));
            deviceToStart.Thread.Start();
            Console.WriteLine($"\nDispositivo {wavyID} iniciado com sucesso!");
        }

        static void IniciarTodasWavys()
        {
            Console.WriteLine("\nIniciando todas as Wavys...");
            bool algumaIniciada = false;

            foreach (var devicePair in dispositivos)
            {
                if (!devicePair.Value.IsRunning)
                {
                    devicePair.Value.IsRunning = true;
                    devicePair.Value.Thread = new Thread(() => 
                        IniciarWavy(devicePair.Key, "acel,gyro,status,hidrofone,transdutor,camera"));
                    devicePair.Value.Thread.Start();
                    Console.WriteLine($"Dispositivo {devicePair.Key} iniciado com sucesso!");
                    algumaIniciada = true;
                }
                else
                {
                    Console.WriteLine($"Dispositivo {devicePair.Key} já estava em execução!");
                }
            }

            if (!algumaIniciada)
            {
                Console.WriteLine("Todas as Wavys já estavam em execução!");
            }
        }

        static void PararDispositivo()
        {
            Console.WriteLine("\nDispositivos em execução:");
            var wavysAtivas = new List<string>();
            
            foreach (var deviceItem in dispositivos)
            {
                if (deviceItem.Value.IsRunning)
                {
                    wavysAtivas.Add(deviceItem.Key);
                }
            }

            if (wavysAtivas.Count == 0)
            {
                Console.WriteLine("Nenhum dispositivo em execução.");
                return;
            }

            for (int i = 0; i < wavysAtivas.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {wavysAtivas[i]}");
            }

            Console.Write("\nDigite o número (1-{0}) ou 0 para cancelar: ", wavysAtivas.Count);
            string? opcao = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(opcao) || !int.TryParse(opcao, out int escolha) || 
                escolha < 0 || escolha > wavysAtivas.Count)
            {
                Console.WriteLine("\nOpção inválida!");
                return;
            }

            if (escolha == 0)
            {
                Console.WriteLine("\nOperação cancelada.");
                return;
            }

            string wavyID = wavysAtivas[escolha - 1];
            if (dispositivos.TryGetValue(wavyID, out WavyDevice? deviceToStop))
            {
                Console.WriteLine($"\nParando o dispositivo {wavyID}...");
                
                deviceToStop.IsRunning = false;
                
                if (deviceToStop.Thread != null && deviceToStop.Thread.IsAlive)
                {
                    Console.WriteLine("Aguardando o dispositivo encerrar...");
                    deviceToStop.Thread.Join();
                }

                publisher.PublishStatus(wavyID, "desativada");
                Console.WriteLine($"Dispositivo {wavyID} parado com sucesso!");
            }
        }

        static void PararTodasWavys()
        {
            Console.WriteLine("\nParando todas as Wavys em execução...");
            bool algumaParada = false;

            foreach (var devicePair in dispositivos)
            {
                if (devicePair.Value.IsRunning)
                {
                    devicePair.Value.IsRunning = false;
                    if (devicePair.Value.Thread != null && devicePair.Value.Thread.IsAlive)
                    {
                        Console.WriteLine($"Aguardando {devicePair.Key} encerrar...");
                        devicePair.Value.Thread.Join();
                    }
                    publisher.PublishStatus(devicePair.Key, "desativada");
                    Console.WriteLine($"Dispositivo {devicePair.Key} parado com sucesso!");
                    algumaParada = true;
                }
            }

            if (!algumaParada)
            {
                Console.WriteLine("Nenhuma Wavy estava em execução!");
            }
        }

        static void ListarDispositivos()
        {
            Console.WriteLine("\nStatus dos dispositivos:");
            foreach (var deviceItem in dispositivos)
            {
                Console.WriteLine($"{deviceItem.Key} - {(deviceItem.Value.IsRunning ? "Em execução" : "Parado")}");
            }
        }

        static void EnviarDadoManual()
        {
            Console.WriteLine("\nDispositivos disponíveis:");
            foreach (var deviceItem in dispositivos)
            {
                Console.WriteLine($"{deviceItem.Key} - {(deviceItem.Value.IsRunning ? "Em execução" : "Parado")}");
            }

            Console.Write("\nDigite o ID do dispositivo: ");
            string? wavyID = Console.ReadLine()?.Trim().ToUpper();

            if (string.IsNullOrEmpty(wavyID) || !dispositivos.TryGetValue(wavyID, out WavyDevice? selectedDevice))
            {
                Console.WriteLine("\nDispositivo não encontrado!");
                return;
            }

            Console.WriteLine("\nTipos de dados disponíveis:");
            Console.WriteLine("1. Acelerômetro (acel)");
            Console.WriteLine("2. Giroscópio (gyro)");
            Console.WriteLine("3. Status");
            Console.WriteLine("4. Hidrofone");
            Console.WriteLine("5. Transdutor");
            Console.WriteLine("6. Câmera");

            Console.Write("\nEscolha o tipo de dado: ");
            string? opcao = Console.ReadLine()?.Trim();

            string tipoDado = opcao switch
            {
                "1" => "acel",
                "2" => "gyro",
                "3" => "status",
                "4" => "hidrofone",
                "5" => "transdutor",
                "6" => "camera",
                _ => ""
            };

            if (string.IsNullOrEmpty(tipoDado))
            {
                Console.WriteLine("\nTipo de dado inválido!");
                return;
            }

            Console.Write("\nDigite o valor para enviar: ");
            string? valor = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(valor))
            {
                Console.WriteLine("\nValor inválido!");
                return;
            }

            publisher.PublishData(wavyID, tipoDado, valor);
            Console.WriteLine($"\nDado enviado: {tipoDado}={valor}");
        }

        static void IniciarWavy(string wavyID, string tiposDeDados)
        {
            try
            {
                LogMensagem(wavyID, "Iniciando dispositivo...");

                // Publicar status inicial
                publisher.PublishStatus(wavyID, "associada");
                Thread.Sleep(100);

                // Registrar tipos de dados
                foreach (var tipo in tiposDeDados.Split(','))
                {
                    publisher.PublishData(wavyID, "config", tipo.Trim());
                }
                Thread.Sleep(100);

                // Atualizar status para operação
                publisher.PublishStatus(wavyID, "operacao");

                // Iniciar envio de dados simulados
                EnviarDadosSimulados(wavyID, tiposDeDados);
            }
            catch (Exception ex)
            {
                LogMensagem(wavyID, $"Erro geral: {ex.Message}");
            }
        }

        static void EnviarDadosSimulados(string wavyID, string tiposDeDados)
        {
            try
            {
                Random rand = new Random(Guid.NewGuid().GetHashCode());
                LogMensagem(wavyID, "Iniciando envio de dados simulados...");

                int contador = 0;
                WavyDevice? device = null;
                if (!dispositivos.TryGetValue(wavyID, out device))
                {
                    LogMensagem(wavyID, "Dispositivo não encontrado no dicionário!");
                    return;
                }

                while (contador < 50 && device.IsRunning)
                {
                    foreach (string tipo in tiposDeDados.Split(','))
                    {
                        if (!device.IsRunning) break;

                        string tipoTrimado = tipo.Trim();
                        string valorSimulado = GerarValorSimulado(tipoTrimado, rand);

                        publisher.PublishData(wavyID, tipoTrimado, valorSimulado);

                        if (!device.IsRunning) break;
                        Thread.Sleep(500);
                    }

                    if (!device.IsRunning) break;
                    contador++;
                }

                if (device.IsRunning)
                {
                    publisher.PublishStatus(wavyID, "desativada");
                }
                else
                {
                    LogMensagem(wavyID, "Execução interrompida pelo usuário.");
                }
            }
            catch (Exception ex)
            {
                LogMensagem(wavyID, $"Erro no envio de dados: {ex.Message}");
            }
        }

        static string GerarValorSimulado(string tipo, Random rand)
        {
            return tipo switch
            {
                "acel" => rand.NextDouble().ToString("F3", CultureInfo.InvariantCulture),
                "gyro" => rand.NextDouble().ToString("F3", CultureInfo.InvariantCulture),
                "status" => rand.Next(0, 2).ToString(),
                "hidrofone" => rand.Next(0, 100).ToString(),
                "transdutor" => rand.Next(0, 100).ToString(),
                "camera" => rand.Next(0, 2).ToString(),
                _ => rand.NextDouble().ToString("F3", CultureInfo.InvariantCulture)
            };
        }

        static void LogMensagem(string wavyID, string mensagem)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{wavyID}] ");
                Console.ResetColor();
                Console.WriteLine(mensagem);
            }
        }
    }
}

