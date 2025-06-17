using System;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Collections.Generic;

namespace PreProcessamentoRPC
{
    [ServiceContract]
    public interface IPreProcessamentoService
    {
        [OperationContract]
        Task<string> ConverterFormato(string dados, string formatoOrigem, string formatoDestino);

        [OperationContract]
        Task<string> UniformizarDados(string dados, string formatoOrigem);

        [OperationContract]
        Task<string> ProcessarDadosCompleto(string dados, string formatoOrigem, string formatoDestino);

        [OperationContract]
        Task<Dictionary<string, double>> ObterTaxasAmostragem();

        [OperationContract]
        Task<bool> AtualizarTaxaAmostragem(string tipoSensor, double novaTaxa);

        [OperationContract]
        Task<string> AnalisarDados(string dados, string tipoAnalise);

        [OperationContract]
        Task<string> SubmeterAnaliseHPC(string dados, string tipoAnalise, Dictionary<string, object> parametros);

        [OperationContract]
        Task<string> ObterStatusAnaliseHPC(string jobId);
    }
} 