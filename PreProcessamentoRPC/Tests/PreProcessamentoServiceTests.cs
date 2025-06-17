using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using System.Text.Json;

namespace PreProcessamentoRPC.Tests
{
    [TestClass]
    public class PreProcessamentoServiceTests
    {
        private PreProcessamentoService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new PreProcessamentoService();
        }

        [TestMethod]
        public async Task ConverterFormato_JSON_Para_CSV_Sucesso()
        {
            // Arrange
            string dadosJSON = "{\"sensor\":\"WAVY001\",\"valor\":25.5}";

            // Act
            string resultado = await _service.ConverterFormato(dadosJSON, "JSON", "CSV");

            // Assert
            Assert.IsTrue(resultado.Contains("WAVY001"));
            Assert.IsTrue(resultado.Contains("25.5"));
        }

        [TestMethod]
        public async Task UniformizarDados_TaxaCorreta_Sucesso()
        {
            // Arrange
            string dados = "{\"sensor\":\"WAVY001\",\"tipo\":\"acel\",\"valor\":25.5,\"timestamp\":\"2024-03-20T10:00:00Z\"}";

            // Act
            string resultado = await _service.UniformizarDados(dados, "JSON");
            var resultadoObj = JsonSerializer.Deserialize<dynamic>(resultado);

            // Assert
            Assert.IsNotNull(resultadoObj);
            Assert.AreEqual(1000, resultadoObj.GetProperty("taxaAmostragem").GetInt32());
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public async Task ConverterFormato_DadosInvalidos_LancaExcecao()
        {
            // Arrange
            string dadosInvalidos = "dados{invalidos}";

            // Act & Assert
            await _service.ConverterFormato(dadosInvalidos, "JSON", "CSV");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service.Dispose();
        }
    }
} 