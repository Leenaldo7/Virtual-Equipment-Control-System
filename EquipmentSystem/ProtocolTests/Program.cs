using System;
using System.Text;

class Program
{
    static void Main()
    {
        var framer = new StxEtxFramer();

        // 1) 정상 프레임 1개
        var bytes = Encoding.UTF8.GetBytes("\x02START|A|100\x03");
        var frames = framer.Feed(bytes, out var warn);

        Console.WriteLine($"frames={frames.Count}, warn={warn}");
        var body = Encoding.UTF8.GetString(frames[0]);
        Console.WriteLine($"body={body}");

        if (PacketParser.TryParse(body, out var pkt, out var err))
            Console.WriteLine($"packet OK: {pkt}");
        else
            Console.WriteLine($"packet FAIL: {err}");

        // 2) TCP처럼 쪼개져 들어오는 경우
        framer.Reset();
        var part1 = Encoding.UTF8.GetBytes("\x02STA");
        var part2 = Encoding.UTF8.GetBytes("RT|A|100\x03");

        var f1 = framer.Feed(part1, out warn);
        Console.WriteLine($"part1 frames={f1.Count}"); // 0 기대

        var f2 = framer.Feed(part2, out warn);
        Console.WriteLine($"part2 frames={f2.Count}"); // 1 기대

        var body2 = Encoding.UTF8.GetString(f2[0]);
        Console.WriteLine($"body2={body2}");

        if (PacketParser.TryParse(body2, out var pkt2, out var err2))
            Console.WriteLine($"packet2 OK: {pkt2}");
        else
            Console.WriteLine($"packet2 FAIL: {err2}");

        // 3) 비정상: STX 없이 ETX만 / 커맨드 이상 / START 파라미터 이상
        Console.WriteLine("\n--- invalid tests ---");

        framer.Reset();
        var junk = Encoding.UTF8.GetBytes("HELLO\x03\x02BAD|X\x03\x02START|A|NOPE\x03");
        var fr = framer.Feed(junk, out warn);
        Console.WriteLine($"invalid frames={fr.Count}, warn={warn}");

        foreach (var b in fr)
        {
            var s = Encoding.UTF8.GetString(b);
            Console.WriteLine($"body={s}");
            if (!PacketParser.TryParse(s, out var p, out var e))
                Console.WriteLine($" -> parse FAIL: {e}");
            else
                Console.WriteLine($" -> parse OK: {p}");
        }
    }
}
