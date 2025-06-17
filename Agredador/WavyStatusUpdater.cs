using System;
using System.Collections.Generic;
using System.Linq;
using Agregador;

class WavyStatusUpdater
{
    private readonly ConfigLoader configLoader;
    private readonly object statusLock = new object();

    public WavyStatusUpdater(ConfigLoader configLoader)
    {
        this.configLoader = configLoader;
    }

    public bool UpdateStatus(string wavyId, string newStatus)
    {
        lock (statusLock)
        {
            // Verificar se a WAVY existe nas configurações
            if (!configLoader.WavyConfigs.ContainsKey(wavyId))
            {
                Console.WriteLine($"WAVY {wavyId} não encontrada nas configurações");
                return false;
            }

            // Verificar se o novo status é válido
            if (!IsStatusValid(newStatus))
            {
                Console.WriteLine($"Status inválido: {newStatus}");
                return false;
            }

            // Verificar se a transição de status é válida
            string currentStatus = configLoader.WavyConfigs[wavyId].Status;
            if (!IsValidStatusTransition(currentStatus, newStatus))
            {
                Console.WriteLine($"Transição de status inválida: {currentStatus} -> {newStatus}");
                return false;
            }

            // Atualizar o status da WAVY
            Console.WriteLine($"Atualizando status da WAVY {wavyId}: {currentStatus} -> {newStatus}");
            return configLoader.UpdateWavyStatus(wavyId, newStatus);
        }
    }

    private bool IsStatusValid(string status)
    {
        string[] validStatus = { "associada", "operacao", "manutencao", "desativada" };
        foreach (string validItem in validStatus)
        {
            if (validItem.Equals(status, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool IsValidStatusTransition(string currentStatus, string newStatus)
    {
        // Regras de transição de status
        // Qualquer status pode ir para "desativada"
        if (newStatus.Equals("desativada", StringComparison.OrdinalIgnoreCase))
            return true;

        // "associada" pode ir para "operacao" ou "manutencao"
        if (currentStatus.Equals("associada", StringComparison.OrdinalIgnoreCase))
        {
            return newStatus.Equals("operacao", StringComparison.OrdinalIgnoreCase) ||
                   newStatus.Equals("manutencao", StringComparison.OrdinalIgnoreCase);
        }

        // "operacao" pode ir para "manutencao"
        if (currentStatus.Equals("operacao", StringComparison.OrdinalIgnoreCase))
        {
            return newStatus.Equals("manutencao", StringComparison.OrdinalIgnoreCase);
        }

        // "manutencao" pode ir para "operacao"
        if (currentStatus.Equals("manutencao", StringComparison.OrdinalIgnoreCase))
        {
            return newStatus.Equals("operacao", StringComparison.OrdinalIgnoreCase);
        }

        // Qualquer outra transição é inválida
        return false;
    }

    public string GetWavyStatus(string wavyId)
    {
        lock (statusLock)
        {
            if (configLoader.WavyConfigs.ContainsKey(wavyId))
                return configLoader.WavyConfigs[wavyId].Status;

            return "desconhecido";
        }
    }

    public bool IsWavyRegistered(string wavyId)
    {
        lock (statusLock)
        {
            return configLoader.WavyConfigs.ContainsKey(wavyId);
        }
    }

    // Espacamento
    public void LogStatusChange(string wavyId, string status)
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"Status da WAVY {wavyId} atualizado para {status}");
        Console.WriteLine();
    }

    public bool RegisterWavy(string wavyId, string dataTypes)
    {
        lock (statusLock)
        {
            // Se a WAVY já existe, atualizar apenas os tipos de dados
            if (configLoader.WavyConfigs.ContainsKey(wavyId))
            {
                // Transformando a string "dataTypes" em uma lista de strings
                List<string> dataTypesList = dataTypes.Split(',').ToList();

                // Chamar o método RegisterWavy passando a lista de dados
                configLoader.RegisterWavy(wavyId, dataTypesList);

                // Atualizar o LastSync
                configLoader.WavyConfigs[wavyId].LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Atualizar o status da WAVY para "associada"
                return configLoader.UpdateWavyStatus(wavyId, "associada");
            }

            // Caso contrário, registrar nova WAVY
            List<string> newDataTypesList = dataTypes.Split(',').ToList();
            return configLoader.RegisterWavy(wavyId, newDataTypesList);
        }
    }
}
