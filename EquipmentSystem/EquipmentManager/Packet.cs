using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Packet
{
    public string Command { get; }
    public IReadOnlyList<string> Params { get; }

    public Packet(string command, IEnumerable<string>? @params = null)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Params = (@params ?? Enumerable.Empty<string>()).ToList();
    }

    public override string ToString()
        => Params.Count == 0 ? Command : $"{Command} | {string.Join(", ", Params)}";
}
