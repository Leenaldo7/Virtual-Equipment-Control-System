using System;
using System.Collections.Generic;

public static class PacketParser
{
    // 허용 커맨드(지금 단계)
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "STATUS", "START", "STOP"
    };

    /// <summary>
    /// Body 문자열: "COMMAND|p1|p2..."
    /// </summary>
    public static bool TryParse(string body, out Packet? packet, out string? error)
    {
        packet = null;
        error = null;

        if (body == null)
        {
            error = "Body is null.";
            return false;
        }

        body = body.Trim();
        if (body.Length == 0)
        {
            error = "Body is empty.";
            return false;
        }

        // split
        var parts = body.Split('|', StringSplitOptions.None);

        // command
        var cmd = parts[0].Trim();
        if (cmd.Length == 0)
        {
            error = "Command is empty.";
            return false;
        }

        if (!Allowed.Contains(cmd))
        {
            error = $"Unknown command: {cmd}";
            return false;
        }

        // params
        var list = new List<string>();
        for (int i = 1; i < parts.Length; i++)
            list.Add(parts[i].Trim());

        // 커맨드별 파라미터 규칙(지금 단계에서 최소한만)
        if (cmd.Equals("STATUS", StringComparison.OrdinalIgnoreCase))
        {
            if (list.Count != 0)
            {
                error = "STATUS must have 0 params.";
                return false;
            }
        }
        else if (cmd.Equals("STOP", StringComparison.OrdinalIgnoreCase))
        {
            if (list.Count != 0)
            {
                error = "STOP must have 0 params.";
                return false;
            }
        }
        else if (cmd.Equals("START", StringComparison.OrdinalIgnoreCase))
        {
            // 예: START|A|100
            if (list.Count != 2)
            {
                error = "START must have 2 params: START|<mode>|<value>";
                return false;
            }

            // value는 숫자라고 가정(너가 스펙에 A,100 예시 줬으니까)
            if (!int.TryParse(list[1], out _))
            {
                error = $"START param2 must be int. Got: {list[1]}";
                return false;
            }
        }

        packet = new Packet(cmd.ToUpperInvariant(), list);
        return true;
    }
}
