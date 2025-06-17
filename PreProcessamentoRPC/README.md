# Serviço de Pré-processamento RPC

## Visão Geral
Este serviço fornece funcionalidades de pré-processamento de dados via RPC para o sistema de monitoramento oceânico.

## Endpoints RPC

### 1. ConverterFormato
Converte dados entre diferentes formatos.

**Parâmetros:**
- `dados`: String com os dados a serem convertidos
- `formatoOrigem`: Formato original dos dados ("JSON", "CSV", "XML", "TEXTO")
- `formatoDestino`: Formato desejado para conversão

**Exemplo:**
```csharp
string resultado = await client.ConverterFormato(dadosJSON, "JSON", "CSV");
```

### 2. UniformizarDados
Uniformiza a taxa de amostragem dos dados.

**Parâmetros:**
- `dados`: String com os dados a serem uniformizados
- `formatoOrigem`: Formato dos dados de entrada

**Exemplo:**
```csharp
string resultado = await client.UniformizarDados(dadosJSON, "JSON");
```

## Formatos Suportados

### JSON
```json
{
    "sensor": "WAVY001",
    "tipo": "temperatura",
    "valor": 25.5,
    "timestamp": "2024-03-20T10:00:00Z"
}
```

### CSV
```
sensor,tipo,valor,timestamp
WAVY001,temperatura,25.5,2024-03-20T10:00:00Z
```

### XML
```xml
<leitura>
    <sensor>WAVY001</sensor>
    <tipo>temperatura</tipo>
    <valor>25.5</valor>
    <timestamp>2024-03-20T10:00:00Z</timestamp>
</leitura>
```

## Taxas de Amostragem Padrão
- Acelerômetro: 10Hz
- Giroscópio: 10Hz
- Status: 1Hz
- Hidrofone: 20Hz
- Transdutor: 5Hz
- Câmera: 1Hz

## Configuração
O serviço utiliza as seguintes configurações padrão:
- Host RabbitMQ: localhost
- Porta RabbitMQ: 5672
- Fila RPC: preprocessamento_rpc_queue

## Tratamento de Erros
O serviço implementa os seguintes tipos de erros:
- FormatException: Erro na conversão de formato
- TimeoutException: Timeout na comunicação RPC
- ValidationException: Dados inválidos 