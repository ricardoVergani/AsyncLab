using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

const int PBKDF2_ITERATIONS = 50_000;
const int HASH_BYTES = 32;
const string CSV_URL = "https://www.gov.br/receitafederal/dados/municipios.csv";
const string OUT_DIR_NAME = "mun_hash_por_uf";

string FormatTempo(long ms)
{
    var ts = TimeSpan.FromMilliseconds(ms);
    return $"{ts.Minutes}m {ts.Seconds}s {ts.Milliseconds}ms";
}

var sw = Stopwatch.StartNew();

string baseDir = Directory.GetCurrentDirectory();
string tempCsvPath = Path.Combine(baseDir, "municipios.csv");
string outRoot = Path.Combine(baseDir, OUT_DIR_NAME);

Console.WriteLine("Baixando CSV de municípios (Receita Federal) ...");
using (var httpClient = new HttpClient())
{
    var csvData = await httpClient.GetByteArrayAsync(CSV_URL);
    await File.WriteAllBytesAsync(tempCsvPath, csvData);
}

Console.WriteLine("Lendo e parseando o CSV ...");
var linhas = await File.ReadAllLinesAsync(tempCsvPath, Encoding.UTF8);
if (linhas.Length == 0)
{
    Console.WriteLine("Arquivo CSV vazio.");
    return;
}

int startIndex = (linhas[0].Contains("IBGE", StringComparison.OrdinalIgnoreCase) ||
                  linhas[0].Contains("UF", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;

var municipios = new List<Municipio>(linhas.Length - startIndex);

for (int i = startIndex; i < linhas.Length; i++)
{
    var linha = (linhas[i] ?? "").Trim();
    if (string.IsNullOrWhiteSpace(linha)) continue;

    var parts = linha.Split(';');
    if (parts.Length < 5) continue;

    municipios.Add(new Municipio
    {
        Tom = Util.San(parts[0]),
        Ibge = Util.San(parts[1]),
        NomeTom = Util.San(parts[2]),
        NomeIbge = Util.San(parts[3]),
        Uf = Util.San(parts[4]).ToUpperInvariant()
    });
}

Console.WriteLine($"Registros lidos: {municipios.Count}");

// Agrupar por UF
var porUf = municipios
    .GroupBy(m => m.Uf)
    .Where(g => !string.Equals(g.Key, "EX", StringComparison.OrdinalIgnoreCase))
    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
    .ToList();

Directory.CreateDirectory(outRoot);
Console.WriteLine("Calculando hash por município e gerando arquivos por UF ...");

// Paralelismo por UF
await Parallel.ForEachAsync(porUf, async (grupoUf, ct) =>
{
    string uf = grupoUf.Key;
    var listaUf = grupoUf.ToList();

    // Ordena para saída consistente
    listaUf.Sort((a, b) => string.Compare(a.NomePreferido, b.NomePreferido, StringComparison.OrdinalIgnoreCase));

    Console.WriteLine($"Processando UF: {uf} ({listaUf.Count} municípios)");
    var swUf = Stopwatch.StartNew();

    string outCsvPath = Path.Combine(outRoot, $"municipios_hash_{uf}.csv");
    string outJsonPath = Path.Combine(outRoot, $"municipios_hash_{uf}.json");

    var jsonList = new ConcurrentBag<object>();
    var csvLines = new ConcurrentBag<string>();

    csvLines.Add("TOM;IBGE;NomeTOM;NomeIBGE;UF;Hash");

    // Paralelismo interno por município
    await Task.WhenAll(listaUf.Select(async m =>
    {
        string password = m.ToConcatenatedString();
        byte[] salt = Util.BuildSalt(m.Ibge);
        string hashHex = await Task.Run(() =>
            Util.DeriveHashHex(password, salt, PBKDF2_ITERATIONS, HASH_BYTES)
        );

        string linhaCsv = $"{m.Tom};{m.Ibge};{m.NomeTom};{m.NomeIbge};{m.Uf};{hashHex}";
        csvLines.Add(linhaCsv);

        jsonList.Add(new
        {
            m.Tom,
            m.Ibge,
            m.NomeTom,
            m.NomeIbge,
            m.Uf,
            Hash = hashHex
        });
    }));

    // Escreve CSV
    await File.WriteAllLinesAsync(outCsvPath, csvLines.OrderBy(l => l), Encoding.UTF8);

    // Escreve JSON
    var json = JsonSerializer.Serialize(jsonList.OrderBy(j => ((dynamic)j).NomeTom), new JsonSerializerOptions
    {
        WriteIndented = true
    });
    await File.WriteAllTextAsync(outJsonPath, json, Encoding.UTF8);

    swUf.Stop();
    Console.WriteLine($"UF {uf} concluída. Tempo total UF: {FormatTempo(swUf.ElapsedMilliseconds)}");
});

sw.Stop();
Console.WriteLine();
Console.WriteLine("===== RESUMO =====");
Console.WriteLine($"UFs geradas: {porUf.Count}");
Console.WriteLine($"Pasta de saída: {outRoot}");
Console.WriteLine($"Tempo total: {FormatTempo(sw.ElapsedMilliseconds)} ({sw.Elapsed})");
