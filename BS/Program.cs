using BS.Infrastructure;
using BS.Models;
using BS.Services;
using System.Diagnostics;

namespace BS;

internal static class Program
{
    private static int Main(string[] args)
    {
        var config = ParseArgs(args);
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            SafeCancel(cts, "Ctrl+C");
        };

        if (config.AutoCancelMs is int ms)
        {
            cts.CancelAfter(ms);
            Console.WriteLine($"[INIT] Автоматическая отмена через {ms} мс.");
        }

        var sw = Stopwatch.StartNew();
        ThreadPool.GetAvailableThreads(out int availStart, out int ioStart);
        Console.WriteLine($"[INIT] Доступных рабочих потоков пула в начале: {availStart}, IO: {ioStart}");

        using var ctx = new StationContext(config, cts);
        var processor = new CellProcessor(ctx);

        var cells = BuildBatch(config);
        ctx.Logger.Log(null, $"Партия создана: {cells.Count} ячеек, слотов={config.ChargerSlots}");


        foreach (var cell in cells)
        {
            var c = cell;
            ThreadPool.QueueUserWorkItem(processor.Process, c);
            ctx.Logger.Log(c.Id, "Ячейка поставлена в очередь");
        }


        bool finished;
        try
        {
            finished = ctx.Countdown.Wait(TimeSpan.FromSeconds(10),
                cts.Token.IsCancellationRequested ? cts.Token : CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            finished = false;
        }


        if (!finished && cts.IsCancellationRequested)
        {
            Console.WriteLine("[MAIN] Запрошена остановка. Ожидание завершения рабочих элементов...");
            finished = ctx.Countdown.Wait(TimeSpan.FromSeconds(10));
            if (!finished)
                Console.WriteLine("[MAIN] ОШИБКА: рабочие элементы не завершились за 10 секунд.");
        }

        sw.Stop();

        ThreadPool.GetAvailableThreads(out int availEnd, out int ioEnd);
        Console.WriteLine($"[INIT] Доступных рабочих потоков пула в конце: {availEnd}, IO: {ioEnd}");

        PrintSummary(ctx, cells.Count, sw.Elapsed);
        return 0;
    }

    private static void SafeCancel(CancellationTokenSource cts, string source)
    {
        try
        {
            Console.WriteLine($"[MAIN] Запрошена аварийная остановка ({source}).");
            cts.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    private static List<Cell> BuildBatch(StationConfig cfg)
    {
        var rnd = new Random(42);
        var list = new List<Cell>(cfg.CellCount);
        for (int i = 0; i < cfg.CellCount; i++)
        {
            list.Add(new Cell
            {
                Id = i + 1,
                DiagnosticIterations = cfg.DiagnosticIterations,
                ChargeDurationMs = rnd.Next(cfg.MinChargeMs, cfg.MaxChargeMs + 1),
                CalibrationDurationMs = rnd.Next(cfg.MinCalibrationMs, cfg.MaxCalibrationMs + 1)
            });
        }
        return list;
    }

    private static void PrintSummary(StationContext ctx, int total, TimeSpan elapsed)
    {
        var r = ctx.Results;
        Console.WriteLine();
        Console.WriteLine("===== ИТОГОВАЯ СТАТИСТИКА =====");
        Console.WriteLine($"Всего ячеек:               {total}");
        Console.WriteLine($"Completed:                 {r.Completed}");
        Console.WriteLine($"Canceled:                  {r.Canceled}");
        Console.WriteLine($"Faulted:                   {r.Faulted}");
        Console.WriteLine($"Сумма статусов:            {r.Completed + r.Canceled + r.Faulted}");
        Console.WriteLine($"Общее время:               {elapsed.TotalMilliseconds:F0} мс");
        Console.WriteLine($"Успешных зарядок:          {r.SuccessfulCharges}");
        Console.WriteLine($"Успешных калибровок:       {r.SuccessfulCalibrations}");
        Console.WriteLine($"Прервано ожиданий зарядки: {r.CanceledChargerWaits}");
        Console.WriteLine($"Прервано ожиданий калибр.: {r.CanceledCalibrationWaits}");
        Console.WriteLine("================================");
    }

    private static StationConfig ParseArgs(string[] args)
    {
        int cells = 20, slots = 4, iters = 100_000;
        int? auto = null;
        foreach (var a in args)
        {
            if (a.StartsWith("--cells=")) cells = int.Parse(a[8..]);
            else if (a.StartsWith("--slots=")) slots = int.Parse(a[8..]);
            else if (a.StartsWith("--iters=")) iters = int.Parse(a[8..]);
            else if (a.StartsWith("--auto-cancel=")) auto = int.Parse(a[14..]);
        }
        return new StationConfig
        {
            CellCount = cells,
            ChargerSlots = slots,
            DiagnosticIterations = iters,
            AutoCancelMs = auto
        };
    }
}
