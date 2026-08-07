using System.Text;
using System.Text.Json;
using Gridfall.Core;
using Gridfall.Core.Content;

namespace Gridfall.Verify;

/// <summary>
/// A recorded command stream plus the per-tick hashes it produced. Map + seed +
/// commands is the entire input to a run, which is what makes replay exact
/// (engine guide 08).
/// </summary>
public sealed class Trace
{
    public required string Map { get; init; }
    public required uint Seed { get; init; }
    public required int Ticks { get; init; }
    public required int CheckpointEvery { get; init; }
    public required List<TraceCommand> Commands { get; init; }
    /// <summary>Checkpoint tick -> hash, as a hex string.</summary>
    public required Dictionary<int, string> Hashes { get; init; }

    public sealed class TraceCommand
    {
        public required int Tick { get; init; }
        public required string Cmd { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public string? Tower { get; init; }
        public int TowerId { get; init; }
    }

    public static Trace Load(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement r = doc.RootElement;

        var commands = new List<TraceCommand>();
        if (r.TryGetProperty("commands", out JsonElement cmds))
        {
            foreach (JsonElement c in cmds.EnumerateArray())
            {
                commands.Add(new TraceCommand
                {
                    Tick = c.GetProperty("tick").GetInt32(),
                    Cmd = c.GetProperty("cmd").GetString()!,
                    X = c.TryGetProperty("x", out var x) ? x.GetInt32() : 0,
                    Y = c.TryGetProperty("y", out var y) ? y.GetInt32() : 0,
                    Tower = c.TryGetProperty("tower", out var t) ? t.GetString() : null,
                    TowerId = c.TryGetProperty("towerId", out var id) ? id.GetInt32() : 0,
                });
            }
        }

        var hashes = new Dictionary<int, string>();
        if (r.TryGetProperty("hashes", out JsonElement h))
            foreach (JsonProperty p in h.EnumerateObject())
                hashes[int.Parse(p.Name)] = p.Value.GetString()!;

        return new Trace
        {
            Map = r.GetProperty("map").GetString()!,
            Seed = r.GetProperty("seed").GetUInt32(),
            Ticks = r.GetProperty("ticks").GetInt32(),
            CheckpointEvery = r.TryGetProperty("checkpointEvery", out var ce) ? ce.GetInt32() : 100,
            Commands = commands,
            Hashes = hashes,
        };
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"map\": \"{Map}\",");
        sb.AppendLine($"  \"seed\": {Seed},");
        sb.AppendLine($"  \"ticks\": {Ticks},");
        sb.AppendLine($"  \"checkpointEvery\": {CheckpointEvery},");

        sb.AppendLine("  \"commands\": [");
        for (int i = 0; i < Commands.Count; i++)
        {
            TraceCommand c = Commands[i];
            string tail = i == Commands.Count - 1 ? "" : ",";
            string extra = c.Cmd switch
            {
                "build" => $", \"x\": {c.X}, \"y\": {c.Y}, \"tower\": \"{c.Tower}\"",
                "sell" => $", \"towerId\": {c.TowerId}",
                _ => "",
            };
            sb.AppendLine($"    {{ \"tick\": {c.Tick}, \"cmd\": \"{c.Cmd}\"{extra} }}{tail}");
        }
        sb.AppendLine("  ],");

        sb.AppendLine("  \"hashes\": {");
        List<int> keys = Hashes.Keys.OrderBy(k => k).ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            string tail = i == keys.Count - 1 ? "" : ",";
            sb.AppendLine($"    \"{keys[i]}\": \"{Hashes[keys[i]]}\"{tail}");
        }
        sb.AppendLine("  }");
        sb.AppendLine("}");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    public void Apply(Sim sim, ContentSet content, int tick)
    {
        foreach (TraceCommand c in Commands)
        {
            if (c.Tick != tick) continue;
            switch (c.Cmd)
            {
                case "build":
                    sim.Enqueue(new BuildCommand(new GridCell(c.X, c.Y), content.TowerIndexOf(c.Tower!)));
                    break;
                case "sell":
                    sim.Enqueue(new SellCommand(c.TowerId));
                    break;
                case "repair":
                    sim.Enqueue(new RepairCommand(c.TowerId));
                    break;
                case "startWave":
                    sim.Enqueue(new StartWaveCommand());
                    break;
                default:
                    throw new InvalidOperationException($"Unknown trace command '{c.Cmd}'");
            }
        }
    }
}
