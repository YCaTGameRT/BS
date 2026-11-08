using BS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BS.Infrastructure;

public sealed class ConsoleLogger
{
    private readonly object _sync = new();

    public void Log(int? cellId, string evt, CellStatus? status = null)
    {
        var line = string.Format(
            "[{0:HH:mm:ss.fff}] [T{1:D3}] [Cell {2}] {3}{4}",
            DateTime.Now,
            Environment.CurrentManagedThreadId,
            cellId?.ToString("D2") ?? "--",
            evt,
            status is null ? string.Empty : $" | Status={status}");

        lock (_sync)
        {
            Console.WriteLine(line);
        }
    }
}
