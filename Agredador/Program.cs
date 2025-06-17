using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Agregador
{
    class Program
    {
        private static readonly ConcurrentDictionary<string, WavyInfo> _wavys = new();
        private static readonly MessageBroker _broker = new();
        private static bool _running = true;
        private static readonly HashSet<string> _selectedDataTypes = new();

        class WavyInfo
        {
            public string ID { get; set; }
            public string Status { get; set; }
            public HashSet<string> DataTypes { get; set; }
            public ConcurrentDictionary<string, string> LastData { get; set; }

            public WavyInfo(string id)
            {
                ID = id;
                Status = "desconhecido";
                DataTypes = new HashSet<string>();
                LastData = new ConcurrentDictionary<string, string>();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== AGREGADOR - Sistema de Monitoramento Oceânico ===\n");
            
            // Inicialmente, inscrever-se para receber status
            _selectedDataTypes.Add("status");
            _broker.Subscribe(ProcessarMensagem, _selectedDataTypes);
            
            Console.WriteLine("Aguardando mensagens das WAVYs...\n");

            while (_running)
            {
                MostrarMenu();
                string? opcao = Console.ReadLine()?.Trim().ToLower();

                switch (opcao)
                {
                    case "1":
                        ListarWavys();
                        break;
                    case "2":
                        MostrarDadosWavy();
                        break;
                    case "3":
                        ConfigurarTiposDados();
                        break;
                    case "4":
                        _running = false;
                        break;
                    default:
                        Console.WriteLine("\nOpção inválida!");
                        break;
                }
            }
        }

        static void MostrarMenu()
        {
            Console.WriteLine("\nMenu Principal:");
            Console.WriteLine("1. Listar WAVYs");
            Console.WriteLine("2. Mostrar dados de uma WAVY");
            Console.WriteLine("3. Configurar tipos de dados para receber");
            Console.WriteLine("4. Sair");
            Console.Write("\nEscolha uma opção: ");
        }

        static void ConfigurarTiposDados()
        {
            Console.WriteLine("\nTipos de dados disponíveis:");
            Console.WriteLine("1. Acelerômetro (acel)");
            Console.WriteLine("2. Giroscópio (gyro)");
            Console.WriteLine("3. Status");
            Console.WriteLine("4. Hidrofone");
            Console.WriteLine("5. Transdutor");
            Console.WriteLine("6. Câmera");
            Console.WriteLine("\nTipos atualmente selecionados: " + string.Join(", ", _selectedDataTypes));
            Console.WriteLine("\nDigite os números dos tipos que deseja receber (separados por vírgula) ou 'todos' para receber tudo:");

            string? input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input)) return;

            _selectedDataTypes.Clear();
            
            if (input == "todos")
            {
                _selectedDataTypes.Add("acel");
                _selectedDataTypes.Add("gyro");
                _selectedDataTypes.Add("status");
                _selectedDataTypes.Add("hidrofone");
                _selectedDataTypes.Add("transdutor");
                _selectedDataTypes.Add("camera");
            }
            else
            {
                var opcoes = input.Split(',');
                foreach (var opcao in opcoes)
                {
                    if (int.TryParse(opcao.Trim(), out int escolha))
                    {
                        string? tipoSelecionado = escolha switch
                        {
                            1 => "acel",
                            2 => "gyro",
                            3 => "status",
                            4 => "hidrofone",
                            5 => "transdutor",
                            6 => "camera",
                            _ => null
                        };

                        if (tipoSelecionado != null)
                        {
                            _selectedDataTypes.Add(tipoSelecionado);
                        }
                    }
                }
            }

            // Sempre adicionar status para manter o controle das WAVYs
            _selectedDataTypes.Add("status");

            // Reinscrever com os novos tipos selecionados
            _broker.Subscribe(ProcessarMensagem, _selectedDataTypes);
            
            Console.WriteLine($"\nAgora recebendo dados dos tipos: {string.Join(", ", _selectedDataTypes)}");
        }

        static void ProcessarMensagem(string wavyId, string dataType, string value)
        {
            var wavy = _wavys.GetOrAdd(wavyId, new WavyInfo(wavyId));

            if (dataType == "status")
            {
                wavy.Status = value;
                Console.WriteLine($"\nStatus da WAVY {wavyId} atualizado para: {value}");
            }
            else
            {
                wavy.DataTypes.Add(dataType);
                wavy.LastData.AddOrUpdate(dataType, value, (_, _) => value);
                Console.WriteLine($"\nDados recebidos da WAVY {wavyId}: {dataType}={value}");
            }
        }

        static void ListarWavys()
        {
            Console.WriteLine("\nWAVYs conectadas:");
            foreach (var wavyPair in _wavys)
            {
                Console.WriteLine($"ID: {wavyPair.Value.ID}");
                Console.WriteLine($"Status: {wavyPair.Value.Status}");
                Console.WriteLine($"Tipos de dados: {string.Join(", ", wavyPair.Value.DataTypes)}");
                Console.WriteLine();
            }
        }

        static void MostrarDadosWavy()
        {
            if (_wavys.Count == 0)
            {
                Console.WriteLine("\nNenhuma WAVY conectada!");
                return;
            }

            Console.WriteLine("\nWAVYs disponíveis:");
            foreach (var wavyPair in _wavys)
            {
                Console.WriteLine($"- {wavyPair.Key}");
            }

            Console.Write("\nDigite o ID da WAVY: ");
            string? wavyId = Console.ReadLine()?.Trim().ToUpper();

            if (string.IsNullOrEmpty(wavyId) || !_wavys.TryGetValue(wavyId, out var wavyInfo))
            {
                Console.WriteLine("WAVY não encontrada!");
                return;
            }

            Console.WriteLine($"\nDados da WAVY {wavyId}:");
            Console.WriteLine($"Status: {wavyInfo.Status}");
            Console.WriteLine("\nÚltimos dados recebidos:");
            foreach (var data in wavyInfo.LastData)
            {
                Console.WriteLine($"{data.Key}: {data.Value}");
            }
        }
    }
}