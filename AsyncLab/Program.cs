using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
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

string ComputeHash(byte[] data)
{
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(data));
}

var sw = Stopwatch.StartNew();

string baseDir = Directory.GetCurrentDirectory();
string tempCsvPath = Path.Combine(baseDir, "municipios.csv");
string hashPath = Path.Combine(baseDir, "municipios.hash");
string outRoot = Path.Combine(baseDir, OUT_DIR_NAME);

Console.WriteLine("Verificando CSV de municípios (Receita Federal) ...");

byte[] novoCsvData;
using (var httpClient = new HttpClient())
{
    novoCsvData = await httpClient.GetByteArrayAsync(CSV_URL);
}

string novoHash = ComputeHash(novoCsvData);
if (File.Exists(tempCsvPath) && File.Exists(hashPath))
{
    string antigoHash = await File.ReadAllTextAsync(hashPath);
    if (antigoHash == novoHash)
    {
        Console.WriteLine("O arquivo local já está atualizado, não será baixado novamente.");
    }
    else
    {
        Console.WriteLine("Arquivo atualizado detectado. Substituindo...");
        await File.WriteAllBytesAsync(tempCsvPath, novoCsvData);
        await File.WriteAllTextAsync(hashPath, novoHash);
    }
}
else
{
    Console.WriteLine("Baixando arquivo pela primeira vez...");
    await File.WriteAllBytesAsync(tempCsvPath, novoCsvData);
    await File.WriteAllTextAsync(hashPath, novoHash);
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


var porUf = municipios
    .GroupBy(m => m.Uf)
    .Where(g => !string.Equals(g.Key, "EX", StringComparison.OrdinalIgnoreCase))
    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
    .ToList();

Directory.CreateDirectory(outRoot);
Console.WriteLine("Calculando hash por município e gerando arquivos por UF ...");

var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };


await Parallel.ForEachAsync(porUf, parallelOpts, async (grupoUf, ct) =>
{
    string uf = grupoUf.Key;
    var listaUf = grupoUf.ToList();
    listaUf.Sort((a, b) => string.Compare(a.NomePreferido, b.NomePreferido, StringComparison.OrdinalIgnoreCase));

    Console.WriteLine($"Processando UF: {uf} ({listaUf.Count} municípios)");
    var swUf = Stopwatch.StartNew();

    string outCsvPath = Path.Combine(outRoot, $"municipios_hash_{uf}.csv");
    string outJsonPath = Path.Combine(outRoot, $"municipios_hash_{uf}.json");
    string outBinPath = Path.Combine(outRoot, $"municipios_hash_{uf}.bin");

    var csvLines = new List<string>(listaUf.Count + 1);
    var jsonList = new List<object>(listaUf.Count);

    csvLines.Add("TOM;IBGE;NomeTOM;NomeIBGE;UF;Hash");


    var bag = new ConcurrentBag<(string csv, object json, Municipio m)>();
    Parallel.ForEach(listaUf, parallelOpts, m =>
    {
        string password = m.ToConcatenatedString();
        byte[] salt = Util.BuildSalt(m.Ibge);
        string hashHex = Util.DeriveHashHex(password, salt, PBKDF2_ITERATIONS, HASH_BYTES);

        string linhaCsv = $"{m.Tom};{m.Ibge};{m.NomeTom};{m.NomeIbge};{m.Uf};{hashHex}";
        var jsonObj = new { m.Tom, m.Ibge, m.NomeTom, m.NomeIbge, m.Uf, Hash = hashHex };

        bag.Add((linhaCsv, jsonObj, m));
    });

    foreach (var item in bag.OrderBy(b => b.m.NomePreferido))
    {
        csvLines.Add(item.csv);
        jsonList.Add(item.json);
    }

    await File.WriteAllLinesAsync(outCsvPath, csvLines, Encoding.UTF8);
    var json = JsonSerializer.Serialize(jsonList);
    await File.WriteAllTextAsync(outJsonPath, json, Encoding.UTF8);

    using (var fs = new FileStream(outBinPath, FileMode.Create, FileAccess.Write))
    using (var bw = new BinaryWriter(fs, Encoding.UTF8))
    {
        foreach (var m in listaUf)
        {
            bw.Write(m.Tom ?? "");
            bw.Write(m.Ibge ?? "");
            bw.Write(m.NomeTom ?? "");
            bw.Write(m.NomeIbge ?? "");
            bw.Write(m.Uf ?? "");
        }
    }

    swUf.Stop();
    Console.WriteLine($"UF {uf} concluída. Tempo total UF: {FormatTempo(swUf.ElapsedMilliseconds)}");
});

sw.Stop();
Console.WriteLine();
Console.WriteLine("===== RESUMO =====");
Console.WriteLine($"UFs geradas: {porUf.Count}");
Console.WriteLine($"Pasta de saída: {outRoot}");
Console.WriteLine($"Tempo total: {FormatTempo(sw.ElapsedMilliseconds)} ({sw.Elapsed})");

//pesquisa para UF
Console.WriteLine("\n===== PESQUISA DE MUNICÍPIOS =====");
while (true)
{
    Console.Write("Digite UF, parte do nome ou codigo (ENTER para sair): ");
    string? q = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(q)) break;

    var resultados = municipios
        .Where(m => m.Uf.Equals(q, StringComparison.OrdinalIgnoreCase)
                 || m.Ibge.Equals(q, StringComparison.OrdinalIgnoreCase)
                 || m.Tom.Equals(q, StringComparison.OrdinalIgnoreCase)
                 || m.NomeIbge.Contains(q, StringComparison.OrdinalIgnoreCase)
                 || m.NomeTom.Contains(q, StringComparison.OrdinalIgnoreCase))
        .Take(20)
        .ToList();

    if (resultados.Count == 0)
        Console.WriteLine("Nenhum município encontrado.");
    else
    {
        foreach (var m in resultados)
            Console.WriteLine($"{m.Uf} - {m.NomeIbge} (IBGE: {m.Ibge}, TOM: {m.Tom})");
    }
}