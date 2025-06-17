using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Agregador
{
    public class ConfigLoader
    {
        private readonly string _wavyConfigFile;
        private readonly string _dataTypeConfigDirectory;

        // Objeto de lock para sincronizar acesso ao arquivo de configuração
        private static readonly object _configFileLock = new object();

        // Propriedades que o DataPreProcessor espera
        public Dictionary<string, WavyConfig> WavyConfigs { get; private set; }
        public Dictionary<string, List<DataProcessingConfig>> DataProcessingConfigs { get; private set; }

        public ConfigLoader(string wavyConfigFile, string dataTypeConfigDirectory)
        {
            _wavyConfigFile = wavyConfigFile;
            _dataTypeConfigDirectory = dataTypeConfigDirectory;

            // Inicializar os dicionários
            WavyConfigs = new Dictionary<string, WavyConfig>();
            DataProcessingConfigs = new Dictionary<string, List<DataProcessingConfig>>();

            // Carregar as configurações
            LoadConfigurations();
        }

        // Método para carregar todas as configurações
        public void LoadConfigurations()
        {
            LoadWavyConfigs();
            LoadDataTypeConfigs();
        }

        // Espacamento
        private void LogConfigMessage(string message)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(message);
            Console.WriteLine();
        }

        // Carrega as configurações das WAVYs com sincronização
        private void LoadWavyConfigs()
        {
            lock (_configFileLock)
            {
                try
                {
                    if (File.Exists(_wavyConfigFile))
                    {
                        string[] lines = File.ReadAllLines(_wavyConfigFile);

                        // Clear dictionary before loading to avoid duplicates
                        WavyConfigs.Clear();

                        foreach (string line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            string[] parts = line.Split(':');
                            if (parts.Length >= 4)
                            {
                                string wavyId = parts[0];
                                WavyConfigs[wavyId] = new WavyConfig
                                {
                                    WavyId = wavyId,
                                    Status = parts[1],
                                    DataTypes = parts[2],
                                    LastSync = parts[3]
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao carregar configurações WAVY: {ex.Message}");
                }
            }
        }

        // Carrega as configurações de processamento por tipo de dados com sincronização
        private void LoadDataTypeConfigs()
        {
            try
            {
                // Não precisa de lock aqui pois estamos apenas lendo os nomes dos arquivos
                string[] files = Directory.GetFiles(_dataTypeConfigDirectory, "*.csv");

                // Clear dictionary before loading to avoid duplicates
                DataProcessingConfigs.Clear();

                foreach (string file in files)
                {
                    if (Path.GetFileName(file) != Path.GetFileName(_wavyConfigFile))
                    {
                        string dataType = Path.GetFileNameWithoutExtension(file);
                        DataProcessingConfigs[dataType] = new List<DataProcessingConfig>();

                        // Lock para acesso ao arquivo individual
                        lock (_configFileLock)
                        {
                            string[] lines = File.ReadAllLines(file);
                            foreach (string line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;

                                string[] parts = line.Split(':');
                                if (parts.Length >= 4)
                                {
                                    DataProcessingConfigs[dataType].Add(new DataProcessingConfig
                                    {
                                        WavyId = parts[0],
                                        PreProcessing = parts[1],
                                        DataVolume = parts[2],
                                        AssociatedServer = parts[3]
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar configurações de tipos de dados: {ex.Message}");
            }
        }

        // Salva as configurações das WAVYs com sincronização
        public void SaveWavyConfigs()
        {
            lock (_configFileLock)
            {
                try
                {
                    List<string> lines = new List<string>();
                    foreach (var config in WavyConfigs.Values)
                    {
                        lines.Add($"{config.WavyId}:{config.Status}:{config.DataTypes}:{config.LastSync}");
                    }

                    // Use mecanismo de arquivo temporário para maior segurança
                    string tempFile = Path.GetTempFileName();
                    File.WriteAllLines(tempFile, lines);

                    // Se chegou aqui, a escrita no arquivo temporário foi bem-sucedida
                    // Agora podemos substituir o arquivo original
                    if (File.Exists(_wavyConfigFile))
                    {
                        File.Delete(_wavyConfigFile);
                    }
                    File.Move(tempFile, _wavyConfigFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao salvar configurações WAVY: {ex.Message}");
                    // Em caso de erro, tentar novamente após um breve período
                    Thread.Sleep(100);
                    try
                    {
                        List<string> lines = new List<string>();
                        foreach (var config in WavyConfigs.Values)
                        {
                            lines.Add($"{config.WavyId}:{config.Status}:{config.DataTypes}:{config.LastSync}");
                        }
                        File.WriteAllLines(_wavyConfigFile, lines);
                    }
                    catch (Exception retryEx)
                    {
                        Console.WriteLine($"Erro na segunda tentativa de salvar configurações WAVY: {retryEx.Message}");
                    }
                }
            }
        }

        // Registra uma WAVY com sincronização
        public bool RegisterWavy(string wavyId, List<string> dataTypes)
        {
            lock (_configFileLock)
            {
                try
                {
                    string joinedDataTypes = string.Join(",", dataTypes);

                    if (WavyConfigs.ContainsKey(wavyId))
                    {
                        WavyConfigs[wavyId].DataTypes = joinedDataTypes;
                        WavyConfigs[wavyId].LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else
                    {
                        WavyConfigs[wavyId] = new WavyConfig
                        {
                            WavyId = wavyId,
                            Status = "associada", // Status inicial
                            DataTypes = joinedDataTypes,
                            LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        };
                    }

                    SaveWavyConfigs(); // Já está usando lock
                    Console.WriteLine($"WAVY {wavyId} registrada com os tipos: {joinedDataTypes}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao registrar WAVY: {ex.Message}");
                    return false;
                }
            }
        }

        // Atualiza o status de uma WAVY com sincronização
        public bool UpdateWavyStatus(string wavyId, string newStatus)
        {
            lock (_configFileLock)
            {
                try
                {
                    if (WavyConfigs.ContainsKey(wavyId))
                    {
                        WavyConfigs[wavyId].Status = newStatus;
                        WavyConfigs[wavyId].LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        SaveWavyConfigs(); // Já está usando lock
                        Console.WriteLine($"Status da WAVY {wavyId} atualizado para {newStatus}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"WAVY {wavyId} não encontrada.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao atualizar status da WAVY: {ex.Message}");
                    return false;
                }
            }
        }
    }

    // Classes de modelo que o DataPreProcessor espera
    public class WavyConfig
    {
        public string WavyId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DataTypes { get; set; } = string.Empty;
        public string LastSync { get; set; } = string.Empty;
    }

    public class DataProcessingConfig
    {
        public string WavyId { get; set; } = string.Empty;
        public string PreProcessing { get; set; } = string.Empty;
        public string DataVolume { get; set; } = string.Empty;
        public string AssociatedServer { get; set; } = string.Empty;
    }
}